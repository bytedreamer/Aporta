using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Services;
using Aporta.Drivers.OSDP.Shared;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Models;
using Z9.Protobuf;
using Z9.Spcore.Proto;
using Aporta.Drivers.OSDP.Shared.Actions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Aporta.Workers;

public class StartupWorker : BackgroundService
{
    private const int DefaultZ9OpenCommunityPort = 9723;
    private static readonly Guid OsdpDriverId = Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5");

    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<StartupWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDataAccess _dataAccess;
    private readonly ExtensionService _extensionService;
    private readonly AccessService _accessService;
    private readonly Z9OpenCommunityProtocolService _z9OpenCommunityProtocolService;
    private readonly EndpointRepository _endpointRepository;
    private readonly Z9DevRepository _z9DevRepository;
    private readonly HashSet<string> _configuredOsdpBuses = new();
    private readonly Dictionary<int, bool> _doorStrikeActive = new();
    private readonly Dictionary<int, bool> _doorForced = new();
    private readonly Dictionary<int, bool> _doorHeld = new();
    private readonly Dictionary<int, bool> _doorUseExtendedTime = new();
    private readonly Dictionary<int, CancellationTokenSource> _doorHeldTimers = new();
    private readonly Dictionary<int, DoorModeType> _doorCurrentMode = new();

    public StartupWorker(IDataAccess dataAccess,
        ExtensionService extensionService,
        AccessService accessService,
        Z9OpenCommunityProtocolService z9OpenCommunityProtocolService,
        IConfiguration configuration,
        ILogger<StartupWorker> logger, IHostApplicationLifetime applicationLifetime)
    {
        _dataAccess = dataAccess;
        _extensionService = extensionService;
        _accessService = accessService;
        _z9OpenCommunityProtocolService = z9OpenCommunityProtocolService;
        _configuration = configuration;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
        _endpointRepository = new EndpointRepository(dataAccess);
        _z9DevRepository = new Z9DevRepository(dataAccess);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Application is starting up services");
        try
        {
            // Check for --cleanDatabase argument to delete existing database
            var cleanDatabase = _configuration.GetValue<bool>("cleanDatabase");
            if (cleanDatabase)
            {
                DeleteDatabaseFile();
            }

            await _dataAccess.UpdateSchema();

            // Check if we're in Z9 Open Community mode or primary config mode
            // BEFORE starting extensions — prevents OSDP from auto-connecting using stale saved configuration
            var z9OpenCommunityHost = _configuration["z9OpenCommunityHost"];
            var isZ9OpenCommunityMode = !string.IsNullOrWhiteSpace(z9OpenCommunityHost);
            var primaryConfigPath = _configuration["primaryConfig"];
            var isPrimaryConfigMode = !string.IsNullOrWhiteSpace(primaryConfigPath);
            if (isZ9OpenCommunityMode || isPrimaryConfigMode)
            {
                await DisableOsdpExtensionForZ9OpenCommunityMode();
            }

            await _extensionService.Startup();
            _accessService.Startup();

            if (isZ9OpenCommunityMode)
            {
                var z9OpenCommunityPort = int.TryParse(_configuration["z9OpenCommunityPort"], out var port)
                    ? port
                    : DefaultZ9OpenCommunityPort;
                var z9OpenCommunityId = _configuration["z9OpenCommunityId"];

                // Wire up door unid lookup for access privilege checking
                _accessService.SetDoorUnidLookup(endpointId =>
                    _z9OpenCommunityProtocolService.GetConfigForEndpoint(endpointId)?.DoorUnid);

                // Wire up strike time lookup for access grant strike duration
                _accessService.SetStrikeTimeLookup(endpointId =>
                {
                    var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(endpointId);
                    return (config?.StrikeTimeMs, config?.ExtendedStrikeTimeMs);
                });

                // Wire up door mode lookup for access decisions
                _accessService.SetDoorModeLookup(endpointId =>
                {
                    var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(endpointId);
                    if (config?.DoorUnid == null) return null;
                    return _doorCurrentMode.TryGetValue(config.DoorUnid.Value, out var mode) ? mode : null;
                });

                // Wire up card data decoder: decode raw bits to credNum for credential matching
                _accessService.SetCardDataDecoder(rawBits =>
                    _z9OpenCommunityProtocolService.DecodeCardRead(rawBits)?.credNum.ToString());

                // Subscribe to OSDP configuration events before starting the service
                _z9OpenCommunityProtocolService.OsdpConfigurationReceived += OnOsdpConfigurationReceived;

                // Subscribe to device online status changes to forward via community protocol
                _extensionService.OnlineStatusChanged += OnOnlineStatusChanged;

                // Subscribe to access decisions to forward via community protocol
                _accessService.AccessDecisionMade += OnAccessDecisionMade;

                // Subscribe to device action requests from the host
                _z9OpenCommunityProtocolService.DevActionRequested += OnDevActionRequested;

                // Subscribe to state changes (door strike output, door contact input) for door state events
                _extensionService.StateChanged += OnStateChanged;

                _z9OpenCommunityProtocolService.Start(z9OpenCommunityHost, z9OpenCommunityPort, z9OpenCommunityId);
            }
            else
            {
                _logger.LogInformation(
                    "Z9/Open Community protocol service not started" +
                    " (use --z9OpenCommunityHost to specify upstream host)");

                // Primary config mode: configure OSDP from a JSON file
                if (isPrimaryConfigMode)
                {
                    await ConfigurePrimaryMode(primaryConfigPath);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "An error occurred during startup");
            _applicationLifetime.StopApplication();
        }

        _logger.LogInformation("Application completed startup routine");

        stoppingToken.Register(() =>
        {
            _logger.LogWarning("Application is shutting down");
            CancelAllDoorHeldTimers();
            _accessService.AccessDecisionMade -= OnAccessDecisionMade;
            _extensionService.StateChanged -= OnStateChanged;
            _z9OpenCommunityProtocolService.DevActionRequested -= OnDevActionRequested;
            _extensionService.OnlineStatusChanged -= OnOnlineStatusChanged;
            _z9OpenCommunityProtocolService.OsdpConfigurationReceived -= OnOsdpConfigurationReceived;
            _z9OpenCommunityProtocolService.Stop();
            _accessService.Shutdown();
            _extensionService.Shutdown();
        });
    }

    private void DeleteDatabaseFile()
    {
        // Database file is stored in Data/Aporta.sqlite relative to the assembly location
        var assemblyPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        var dataDir = System.IO.Path.GetDirectoryName(assemblyPath) ?? Environment.CurrentDirectory;
        var dbPath = System.IO.Path.Combine(dataDir, "Data", "Aporta.sqlite");

        if (System.IO.File.Exists(dbPath))
        {
            _logger.LogInformation("Deleting existing database file: {DbPath}", dbPath);
            System.IO.File.Delete(dbPath);
        }
        else
        {
            _logger.LogDebug("No database file to delete at: {DbPath}", dbPath);
        }
    }

    private async Task DisableOsdpExtensionForZ9OpenCommunityMode()
    {
        // Use direct database access because ExtensionService hasn't been started yet
        // This ensures OSDP extension won't be loaded at startup with stale config
        using var connection = _dataAccess.CreateDbConnection();
        connection.Open();

        var data = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT data FROM extension WHERE id = @id",
            new { id = OsdpDriverId.ToString() });

        if (data == null)
        {
            _logger.LogDebug("OSDP extension not found in database, nothing to disable");
            return;
        }

        _logger.LogInformation(
            "Z9 Open Community mode: Disabling OSDP extension and clearing saved configuration");

        // Set enabled=false AND clear the configuration to prevent stale buses/devices
        // from being loaded when the extension is re-enabled
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        using var doc = JsonDocument.Parse(data);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name == "enabled")
                writer.WriteBoolean("enabled", false);
            else if (prop.Name == "configuration")
                writer.WriteNull("configuration");  // Clear stale config
            else
                prop.WriteTo(writer);
        }
        writer.WriteEndObject();
        writer.Flush();
        var newData = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        await connection.ExecuteAsync(
            "UPDATE extension SET data = @data WHERE id = @id",
            new { id = OsdpDriverId.ToString(), data = newData });
    }

    /// <summary>
    /// Configuration model for --primaryConfig JSON file.
    /// </summary>
    public class PrimaryConfig
    {
        [System.Text.Json.Serialization.JsonPropertyName("readers")]
        public List<PrimaryReaderConfig> Readers { get; set; } = new();
    }

    public class PrimaryReaderConfig
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("host")]
        public string Host { get; set; } = "localhost";

        [System.Text.Json.Serialization.JsonPropertyName("tcpPort")]
        public int TcpPort { get; set; } = 9843;

        [System.Text.Json.Serialization.JsonPropertyName("osdpAddress")]
        public int OsdpAddress { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("baudRate")]
        public int BaudRate { get; set; } = 9600;

        [System.Text.Json.Serialization.JsonPropertyName("strikeOutputNumber")]
        public int? StrikeOutputNumber { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("doorContactInputNumber")]
        public int? DoorContactInputNumber { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("rexInputNumber")]
        public int? RexInputNumber { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("activateStrikeOnRex")]
        public bool ActivateStrikeOnRex { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("strikeTimeMs")]
        public int? StrikeTimeMs { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("extendedStrikeTimeMs")]
        public int? ExtendedStrikeTimeMs { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("heldTimeMs")]
        public int? HeldTimeMs { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("extendedHeldTimeMs")]
        public int? ExtendedHeldTimeMs { get; set; }
    }

    private async Task ConfigurePrimaryMode(string configPath)
    {
        _logger.LogInformation("Loading primary config from {Path}", configPath);

        var json = await System.IO.File.ReadAllTextAsync(configPath);
        var primaryConfig = System.Text.Json.JsonSerializer.Deserialize<PrimaryConfig>(json);

        if (primaryConfig?.Readers == null || primaryConfig.Readers.Count == 0)
        {
            _logger.LogWarning("Primary config has no readers defined");
            return;
        }

        // Wire strike time lookup
        _accessService.SetStrikeTimeLookup(endpointId =>
        {
            var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(endpointId);
            return (config?.StrikeTimeMs, config?.ExtendedStrikeTimeMs);
        });

        // Subscribe to online status changes for auto Door creation
        _extensionService.OnlineStatusChanged += OnOnlineStatusChanged;

        // Subscribe to state changes for door contact/REX/strike tracking
        _extensionService.StateChanged += OnStateChanged;

        // Convert and register each reader config, then configure the OSDP driver
        for (int i = 0; i < primaryConfig.Readers.Count; i++)
        {
            var reader = primaryConfig.Readers[i];
            var osdpConfig = new Z9OpenCommunityProtocolService.OsdpReaderConfig
            {
                Unid = i + 1,
                Name = reader.Name ?? $"Reader {i + 1}",
                Host = reader.Host,
                TcpPort = reader.TcpPort,
                OsdpAddress = reader.OsdpAddress,
                BaudRate = reader.BaudRate,
                StrikeOutputNumber = reader.StrikeOutputNumber,
                DoorContactInputNumber = reader.DoorContactInputNumber,
                RexInputNumber = reader.RexInputNumber,
                ActivateStrikeOnRex = reader.ActivateStrikeOnRex,
                StrikeTimeMs = reader.StrikeTimeMs,
                ExtendedStrikeTimeMs = reader.ExtendedStrikeTimeMs,
                HeldTimeMs = reader.HeldTimeMs,
                ExtendedHeldTimeMs = reader.ExtendedHeldTimeMs
            };

            _z9OpenCommunityProtocolService.RegisterOsdpReaderConfig(osdpConfig);
            await ConfigureOsdpDriver(osdpConfig);
        }

        _logger.LogInformation("Primary config loaded: {Count} reader(s) configured", primaryConfig.Readers.Count);
    }

    private void OnOsdpConfigurationReceived(object sender, Z9OpenCommunityProtocolService.OsdpReaderConfig config)
    {
        Task.Run(async () =>
        {
            try
            {
                await ConfigureOsdpDriver(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure OSDP driver for reader {Name}", config.Name);
            }
        });
    }

    private async Task ConfigureOsdpDriver(Z9OpenCommunityProtocolService.OsdpReaderConfig config)
    {
        _logger.LogInformation(
            "Configuring OSDP driver for reader: {Name} at {Host}:{TcpPort} address={OsdpAddress}",
            config.Name, config.Host, config.TcpPort, config.OsdpAddress);

        // Check if OSDP driver extension is available and enabled
        var extensions = _extensionService.GetExtensions().ToList();
        var osdpExtension = extensions.FirstOrDefault(e => e.Id == OsdpDriverId);

        if (osdpExtension == null)
        {
            _logger.LogWarning("OSDP driver extension not found");
            return;
        }

        if (!osdpExtension.Enabled)
        {
            _logger.LogInformation("Enabling OSDP driver extension");
            await _extensionService.EnableExtension(OsdpDriverId, true);
        }

        // Create a unique bus identifier based on host:port
        var busKey = $"{config.Host}:{config.TcpPort}";

        // Only add the bus once per host:port combination
        if (!_configuredOsdpBuses.Contains(busKey))
        {
            _logger.LogInformation("Adding TCP OSDP bus: {BusKey}", busKey);

            var bus = new Bus
            {
                ConnectionType = ConnectionType.Tcp,
                PortName = busKey,  // Use as identifier
                TcpHost = config.Host,
                TcpPort = config.TcpPort,
                BaudRate = config.BaudRate
            };

            var busAction = new BusAction { Bus = bus };
            await _extensionService.PerformAction(OsdpDriverId, ActionType.AddSerialBus.ToString(),
                JsonConvert.SerializeObject(busAction));

            _configuredOsdpBuses.Add(busKey);
        }

        // Add the device to the bus
        _logger.LogInformation("Adding OSDP device: {Name} address={OsdpAddress}", config.Name, config.OsdpAddress);

        var device = new Device
        {
            Name = config.Name,
            Address = (byte)config.OsdpAddress,
            PortName = busKey,  // Reference to bus
            RequireSecurity = false  // Start without secure channel for testing
        };

        var deviceAction = new DeviceAction { Device = device };
        await _extensionService.PerformAction(OsdpDriverId, ActionType.AddUpdateDevice.ToString(),
            JsonConvert.SerializeObject(deviceAction));

        _logger.LogInformation("OSDP driver configured for reader: {Name}", config.Name);
    }

    private void OnOnlineStatusChanged(object sender, OnlineStatusChangedEventArgs e)
    {
        // Only process reader endpoints (e.g., "localhost:9843:0:R0")
        // Skip output endpoints (e.g., "localhost:9843:0:O0") which share the same prefix
        var endpointId = e.Endpoint?.Id;
        if (string.IsNullOrEmpty(endpointId) || !IsReaderEndpoint(endpointId))
        {
            _logger.LogDebug("Skipping non-reader endpoint {EndpointId}", endpointId);
            return;
        }

        // Get the Z9 config for this endpoint to send event with correct device unid
        var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(endpointId);
        if (config == null)
        {
            _logger.LogDebug("No Z9 config found for endpoint {EndpointId}, skipping event",
                endpointId);
            return;
        }

        _logger.LogInformation("OSDP reader {Name} is now {Status}",
            config.Name, e.IsOnline ? "online" : "offline");

        // When reader comes online, ensure a Door exists for access control decisions
        if (e.IsOnline)
        {
            Task.Run(async () =>
            {
                try
                {
                    await EnsureDoorExistsForReader(endpointId, config.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create door for reader {Name}", config.Name);
                }
            });
        }

        _z9OpenCommunityProtocolService.SendCredReaderOnlineEvent(config, e.IsOnline);
    }

    /// <summary>
    /// Checks if an OSDP endpoint ID represents a reader (vs output, input, etc.).
    /// Reader endpoint IDs end with ":R{number}" (e.g., "localhost:9843:0:R0").
    /// </summary>
    private static bool IsReaderEndpoint(string endpointId)
    {
        var lastColon = endpointId.LastIndexOf(':');
        return lastColon >= 0 && lastColon < endpointId.Length - 1 && endpointId[lastColon + 1] == 'R';
    }

    private async Task EnsureDoorExistsForReader(string driverEndpointId, string readerName)
    {
        // Check if CredReader Dev with this ExternalId already exists — door already exists
        Dev existingCredReader = null;
        for (int retry = 0; retry < 10; retry++)
        {
            existingCredReader = await _z9DevRepository.GetByExternalId(driverEndpointId);
            if (existingCredReader != null)
                break;

            // Endpoint may not be persisted immediately, check if it exists yet
            var endpoint = await _endpointRepository.GetByDriverEndpointId(driverEndpointId);
            if (endpoint != null)
                break; // Endpoint exists but no dev yet — proceed to create

            await Task.Delay(500);  // Wait for endpoint to be persisted
        }

        if (existingCredReader != null && existingCredReader.DevType == DevType.CredReader)
        {
            _logger.LogDebug("CredReader Dev already exists for endpoint {EndpointId}, door exists",
                driverEndpointId);
            return;
        }

        // Create Door Dev tree
        var doorDev = new Dev
        {
            Name = $"Door - {readerName}",
            DevType = DevType.Door,
        };
        SpCoreProtoUtil.InitRequired(doorDev);
        var doorUnid = await _z9DevRepository.Insert(doorDev);

        // Create CredReader child
        var credReaderDev = new Dev
        {
            Name = readerName,
            DevType = DevType.CredReader,
            ExternalId = driverEndpointId,
            LogicalParentUnid = doorUnid,
        };
        SpCoreProtoUtil.InitRequired(credReaderDev);
        var credReaderUnid = await _z9DevRepository.Insert(credReaderDev);
        doorDev.LogicalChildrenUnid.Add(credReaderUnid);

        var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);

        // If the reader has a strike output configured, create Actuator child
        if (config?.StrikeOutputNumber != null)
        {
            var strikeDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:O{config.StrikeOutputNumber}";
            _logger.LogInformation("Looking for strike output endpoint: {StrikeEndpointId}", strikeDriverEndpointId);

            Endpoint strikeEndpoint = null;
            for (int retry = 0; retry < 20; retry++)
            {
                strikeEndpoint = await _endpointRepository.GetByDriverEndpointId(strikeDriverEndpointId);
                if (strikeEndpoint != null)
                    break;
                await Task.Delay(500);
            }

            if (strikeEndpoint != null)
            {
                var strikeDev = new Dev
                {
                    Name = strikeEndpoint.Name,
                    DevType = DevType.Actuator,
                    DevUse = DevUse.ActuatorDoorStrike,
                    ExternalId = strikeDriverEndpointId,
                    LogicalParentUnid = doorUnid,
                };
                SpCoreProtoUtil.InitRequired(strikeDev);
                var strikeUnid = await _z9DevRepository.Insert(strikeDev);
                doorDev.LogicalChildrenUnid.Add(strikeUnid);
                _logger.LogInformation("Created Actuator Dev for door strike {EndpointId}", strikeDriverEndpointId);
            }
            else
            {
                _logger.LogWarning("Strike output endpoint {StrikeEndpointId} not found after retries", strikeDriverEndpointId);
            }
        }

        _logger.LogInformation("Created door '{DoorName}' (unid={DoorUnid}) for reader endpoint {EndpointId}",
            doorDev.Name, doorUnid, driverEndpointId);

        // If the reader has a door contact input configured, create Sensor child
        if (config?.DoorContactInputNumber != null)
        {
            var contactDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:I{config.DoorContactInputNumber}";
            _logger.LogInformation("Looking for door contact input endpoint: {ContactEndpointId}", contactDriverEndpointId);

            Endpoint contactEndpoint = null;
            for (int retry = 0; retry < 20; retry++)
            {
                contactEndpoint = await _endpointRepository.GetByDriverEndpointId(contactDriverEndpointId);
                if (contactEndpoint != null)
                    break;
                await Task.Delay(500);
            }

            if (contactEndpoint != null)
            {
                var contactDev = new Dev
                {
                    Name = contactEndpoint.Name,
                    DevType = DevType.Sensor,
                    DevUse = DevUse.SensorDoorContact,
                    ExternalId = contactDriverEndpointId,
                    LogicalParentUnid = doorUnid,
                };
                SpCoreProtoUtil.InitRequired(contactDev);
                var contactUnid = await _z9DevRepository.Insert(contactDev);
                doorDev.LogicalChildrenUnid.Add(contactUnid);
                _logger.LogInformation("Created Sensor Dev for door contact {EndpointId}", contactDriverEndpointId);
            }
            else
            {
                _logger.LogWarning("Door contact input endpoint {ContactEndpointId} not found after retries", contactDriverEndpointId);
            }
        }

        // If the reader has a REX input configured, create Sensor child
        if (config?.RexInputNumber != null)
        {
            var rexDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:I{config.RexInputNumber}";
            _logger.LogInformation("Looking for REX input endpoint: {RexEndpointId}", rexDriverEndpointId);

            Endpoint rexEndpoint = null;
            for (int retry = 0; retry < 20; retry++)
            {
                rexEndpoint = await _endpointRepository.GetByDriverEndpointId(rexDriverEndpointId);
                if (rexEndpoint != null)
                    break;
                await Task.Delay(500);
            }

            if (rexEndpoint != null)
            {
                var rexDev = new Dev
                {
                    Name = rexEndpoint.Name,
                    DevType = DevType.Sensor,
                    DevUse = DevUse.SensorRex,
                    ExternalId = rexDriverEndpointId,
                    LogicalParentUnid = doorUnid,
                };
                SpCoreProtoUtil.InitRequired(rexDev);
                var rexUnid = await _z9DevRepository.Insert(rexDev);
                doorDev.LogicalChildrenUnid.Add(rexUnid);
                _logger.LogInformation("Created Sensor Dev for REX {EndpointId}", rexDriverEndpointId);
            }
            else
            {
                _logger.LogWarning("REX input endpoint {RexEndpointId} not found after retries", rexDriverEndpointId);
            }
        }

        // Update Door Dev with children
        await _z9DevRepository.Upsert(doorDev);

        // Apply default door mode if configured
        if (config?.DefaultDoorMode != null && config.DoorUnid.HasValue)
        {
            await ApplyDoorMode(config.DoorUnid.Value, config.DefaultDoorMode, config);
        }
    }

    private void OnDevActionRequested(object sender, DevActionReq req)
    {
        if (req.DevActionType == DevActionType.DoorMomentaryUnlock)
        {
            var doorUnid = req.DevUnid;
            _logger.LogInformation("DevActionReq: DOOR_MOMENTARY_UNLOCK for door unid={DoorUnid}", doorUnid);

            Task.Run(async () =>
            {
                try
                {
                    await HandleDoorMomentaryUnlock(doorUnid);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle DOOR_MOMENTARY_UNLOCK for door unid={DoorUnid}", doorUnid);
                }
            });
        }
        else if (req.DevActionType == DevActionType.DoorModeChange)
        {
            var doorUnid = req.DevUnid;
            _logger.LogInformation("DevActionReq: DOOR_MODE_CHANGE for door unid={DoorUnid}", doorUnid);

            Task.Run(async () =>
            {
                try
                {
                    await HandleDoorModeChange(doorUnid, req.DevActionParams);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle DOOR_MODE_CHANGE for door unid={DoorUnid}", doorUnid);
                }
            });
        }
        else
        {
            _logger.LogInformation("DevActionReq: unhandled action type {Type} for device unid={DevUnid}",
                req.DevActionType, req.DevUnid);
        }
    }

    private async Task HandleDoorMomentaryUnlock(int doorUnid)
    {
        // Find the reader config whose DoorUnid matches
        var config = _z9OpenCommunityProtocolService.OsdpReaderConfigs
            .FirstOrDefault(c => c.DoorUnid == doorUnid);

        if (config == null)
        {
            _logger.LogWarning("No reader config found for door unid={DoorUnid}", doorUnid);
            return;
        }

        if (config.StrikeOutputNumber == null)
        {
            _logger.LogWarning("No strike output configured for reader {Name}", config.Name);
            return;
        }

        var strikeDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:O{config.StrikeOutputNumber}";

        var strikeEndpoint = await _endpointRepository.GetByDriverEndpointId(strikeDriverEndpointId);

        if (strikeEndpoint == null)
        {
            _logger.LogWarning("Strike endpoint {EndpointId} not found in database", strikeDriverEndpointId);
            return;
        }

        var controlPoint = _extensionService.GetControlPoint(strikeEndpoint.ExtensionId, strikeEndpoint.DriverEndpointId);
        if (controlPoint == null)
        {
            _logger.LogWarning("No control point found for strike endpoint {EndpointId}", strikeDriverEndpointId);
            return;
        }

        _logger.LogInformation("Activating door strike for door unid={DoorUnid} (endpoint={EndpointId})",
            doorUnid, strikeDriverEndpointId);

        var strikeTimeMs = config.StrikeTimeMs ?? 3000;

        await controlPoint.SetState(true);
        await Task.Delay(TimeSpan.FromMilliseconds(strikeTimeMs));
        await controlPoint.SetState(false);

        _logger.LogInformation("Door strike deactivated for door unid={DoorUnid} (strikeTime={StrikeTimeMs}ms)",
            doorUnid, strikeTimeMs);
    }

    private async Task HandleDoorModeChange(int doorUnid, DevActionParams devActionParams)
    {
        var modeParams = devActionParams?.ExtDoorModeDevActionParams;
        if (modeParams == null)
        {
            _logger.LogWarning("DOOR_MODE_CHANGE missing DoorModeDevActionParams for door unid={DoorUnid}", doorUnid);
            return;
        }

        // Find the reader config for this door
        var config = _z9OpenCommunityProtocolService.OsdpReaderConfigs
            .FirstOrDefault(c => c.DoorUnid == doorUnid);
        if (config == null)
        {
            _logger.LogWarning("No reader config found for door unid={DoorUnid}", doorUnid);
            return;
        }

        // Determine which DoorMode to apply
        DoorMode doorMode;
        if (modeParams.ResetToDefaultCase == DoorModeDevActionParams.ResetToDefaultOneofCase.ResetToDefault
            && modeParams.ResetToDefault)
        {
            doorMode = config.DefaultDoorMode;
            if (doorMode == null)
            {
                _logger.LogWarning("ResetToDefault requested but no default door mode for door unid={DoorUnid}", doorUnid);
                return;
            }
            _logger.LogInformation("Resetting door unid={DoorUnid} to default mode", doorUnid);
        }
        else
        {
            doorMode = modeParams.DoorMode;
            if (doorMode == null)
            {
                _logger.LogWarning("DOOR_MODE_CHANGE has no DoorMode for door unid={DoorUnid}", doorUnid);
                return;
            }
        }

        await ApplyDoorMode(doorUnid, doorMode, config);
    }

    private async Task ApplyDoorMode(int doorUnid, DoorMode doorMode, Z9OpenCommunityProtocolService.OsdpReaderConfig config)
    {
        var doorModeType = CommonDoorModes.DoorModeToDoorModeType(doorMode);
        if (!doorModeType.HasValue)
        {
            _logger.LogWarning("Could not determine DoorModeType for door unid={DoorUnid}", doorUnid);
            return;
        }

        var effectiveMode = doorModeType.Value;

        var previousMode = _doorCurrentMode.TryGetValue(doorUnid, out var prev) ? prev : (DoorModeType?)null;
        _doorCurrentMode[doorUnid] = effectiveMode;

        _logger.LogInformation("Door unid={DoorUnid} mode changed to {Mode}", doorUnid, effectiveMode);

        // Actuate strike based on mode
        if (config.StrikeOutputNumber != null)
        {
            var strikeDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:O{config.StrikeOutputNumber}";
            var strikeEndpoint = await _endpointRepository.GetByDriverEndpointId(strikeDriverEndpointId);

            if (strikeEndpoint != null)
            {
                var controlPoint = _extensionService.GetControlPoint(strikeEndpoint.ExtensionId, strikeEndpoint.DriverEndpointId);
                if (controlPoint != null)
                {
                    if (effectiveMode == DoorModeType.StaticStateUnlocked)
                    {
                        await controlPoint.SetState(true);
                        _z9OpenCommunityProtocolService.SendDoorStateEvent(config, EvtCode.DoorUnlocked);
                    }
                    else if (previousMode == DoorModeType.StaticStateUnlocked)
                    {
                        // Switching away from unlocked — deactivate strike
                        await controlPoint.SetState(false);
                        _z9OpenCommunityProtocolService.SendDoorStateEvent(config, EvtCode.DoorLocked);
                    }
                }
            }
        }

        // Send the door mode event
        var modeEvtCode = effectiveMode switch
        {
            DoorModeType.StaticStateUnlocked => EvtCode.DoorModeStaticStateUnlocked,
            DoorModeType.StaticStateLocked => EvtCode.DoorModeStaticStateLocked,
            DoorModeType.CardOnly => EvtCode.DoorModeCardOnly,
            DoorModeType.CardAndConfirmingPin => EvtCode.DoorModeCardAndConfirmingPin,
            DoorModeType.UniquePinOnly => EvtCode.DoorModeUniquePinOnly,
            DoorModeType.CardOnlyOrUniquePin => EvtCode.DoorModeCardOnlyOrUniquePin,
            _ => EvtCode.DoorModeCardOnly
        };
        _z9OpenCommunityProtocolService.SendDoorStateEvent(config, modeEvtCode);
    }

    private void OnStateChanged(object sender, StateChangedEventArgs e)
    {
        var driverEndpointId = e.Endpoint?.Id;
        if (string.IsNullOrEmpty(driverEndpointId))
            return;

        Task.Run(async () =>
        {
            try
            {
                await HandleStateChanged(driverEndpointId, e.State);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle state change for endpoint {EndpointId}", driverEndpointId);
            }
        });
    }

    private async Task HandleStateChanged(string driverEndpointId, bool state)
    {
        // Look up the Dev by ExternalId (DriverEndpointId)
        var dev = await _z9DevRepository.GetByExternalId(driverEndpointId);
        if (dev == null)
            return;

        // Get the door unid (parent) for state tracking
        var doorUnid = dev.LogicalParentUnidCase == Dev.LogicalParentUnidOneofCase.LogicalParentUnid
            ? dev.LogicalParentUnid : 0;

        // Check if this is a REX sensor
        if (dev.DevType == DevType.Sensor && dev.DevUseCase == Dev.DevUseOneofCase.DevUse && dev.DevUse == DevUse.SensorRex && state)
        {
            var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);
            if (config != null)
            {
                _logger.LogInformation("REX activated for door unid={DoorUnid}", doorUnid);
                _z9OpenCommunityProtocolService.SendDoorStateEvent(config, EvtCode.ExitRequested);

                // Determine REX behavior based on door mode
                var currentMode = doorUnid > 0 && _doorCurrentMode.TryGetValue(doorUnid, out var mode)
                    ? mode : (DoorModeType?)null;

                if (currentMode == DoorModeType.StaticStateUnlocked)
                {
                    // Already unlocked — no need for momentary unlock
                }
                else if (currentMode == DoorModeType.StaticStateLocked)
                {
                    // Door is locked — do not unlock on REX
                    _logger.LogInformation("REX ignored for door unid={DoorUnid} — door is in locked mode", doorUnid);
                }
                else if (config.ActivateStrikeOnRex && config.DoorUnid.HasValue)
                {
                    await HandleDoorMomentaryUnlock(config.DoorUnid.Value);
                }
            }
            return;
        }

        // Check if this is a door strike actuator — track state for forced-open detection
        if (dev.DevType == DevType.Actuator && dev.DevUseCase == Dev.DevUseOneofCase.DevUse && dev.DevUse == DevUse.ActuatorDoorStrike)
        {
            if (doorUnid > 0)
                _doorStrikeActive[doorUnid] = state;
            var evtCode = state ? EvtCode.DoorUnlocked : EvtCode.DoorLocked;
            var config2 = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);
            if (config2 != null)
                _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, evtCode);
            return;
        }

        // Check if this is a door contact sensor
        if (dev.DevType == DevType.Sensor && dev.DevUseCase == Dev.DevUseOneofCase.DevUse && dev.DevUse == DevUse.SensorDoorContact)
        {
            var config2 = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);
            if (config2 == null)
                return;

            if (!state) // Door opened
            {
                _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorOpened);

                _doorStrikeActive.TryGetValue(doorUnid, out var strikeActive);
                if (!strikeActive)
                {
                    _logger.LogWarning("Door forced open: door unid={DoorUnid}", doorUnid);
                    _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorForced);
                    _doorForced[doorUnid] = true;
                }

                StartDoorHeldTimer(doorUnid, dev.Name, config2);
            }
            else // Door closed
            {
                CancelDoorHeldTimer(doorUnid);

                _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorClosed);

                if (_doorForced.TryGetValue(doorUnid, out var wasForced) && wasForced)
                {
                    _logger.LogInformation("Door forced cleared: door unid={DoorUnid}", doorUnid);
                    _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorNotForced);
                    _doorForced[doorUnid] = false;
                }

                if (_doorHeld.TryGetValue(doorUnid, out var wasHeld) && wasHeld)
                {
                    _logger.LogInformation("Door held cleared: door unid={DoorUnid}", doorUnid);
                    _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorNotHeld);
                    _doorHeld[doorUnid] = false;
                }
            }
            return;
        }
    }

    private void OnAccessDecisionMade(object sender, AccessDecisionEventArgs e)
    {
        // Store extended time flag for door held timer (consumed when door opens)
        if (e.IsGranted)
        {
            var readerConfig = _z9OpenCommunityProtocolService.GetConfigForEndpoint(e.EndpointId);
            if (readerConfig?.DoorUnid != null)
            {
                _doorUseExtendedTime[readerConfig.DoorUnid.Value] = e.UseExtendedTime;
            }
        }

        // Get the Z9 config for this endpoint to send event with correct device unid
        var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(e.EndpointId);
        if (config == null)
        {
            _logger.LogDebug("No Z9 config found for endpoint {EndpointId}, skipping access event",
                e.EndpointId);
            return;
        }

        var rawBits = e.CardNumber;

        // Try to decode the raw bits using known card formats
        System.Numerics.BigInteger? credNum = null;
        int? facilityCode = null;
        if (!string.IsNullOrEmpty(rawBits))
        {
            var decoded = _z9OpenCommunityProtocolService.DecodeCardRead(rawBits);
            if (decoded.HasValue)
            {
                credNum = decoded.Value.credNum;
                facilityCode = decoded.Value.facilityCode;
                _logger.LogDebug("Decoded card: credNum={CredNum}, fc={FacilityCode}, format={Format}",
                    credNum, facilityCode, decoded.Value.formatName);
            }
        }

        // Map EventReason to EvtSubCode via shared mapping
        var eventType = e.IsGranted ? EventType.AccessGranted : EventType.AccessDenied;
        var (_, evtSubCode) = EventReasonMapping.ToEvtCodes(eventType, e.Reason);
        var subCode = evtSubCode ?? EvtSubCode.AccessDeniedInactive;

        _logger.LogInformation("Access {Decision} for reader {Name}: person={Person}, reason={Reason}",
            e.IsGranted ? "GRANTED" : "DENIED", config.Name, e.PersonName ?? "(unknown)", e.Reason);

        _z9OpenCommunityProtocolService.SendAccessEvent(
            config,
            isGranted: e.IsGranted,
            credNum: credNum,
            facilityCode: facilityCode,
            rawBits: rawBits,
            subCode: subCode);
    }

    private void StartDoorHeldTimer(int doorId, string doorName, Z9OpenCommunityProtocolService.OsdpReaderConfig config)
    {
        // Pick held time: use extended if last access grant had extDoorTime
        var useExtended = _doorUseExtendedTime.TryGetValue(doorId, out var ext) && ext;
        var heldTimeMs = useExtended && config.ExtendedHeldTimeMs.HasValue
            ? config.ExtendedHeldTimeMs.Value
            : config.HeldTimeMs ?? 0;

        // Clear one-shot extended time flag
        _doorUseExtendedTime.Remove(doorId);

        if (heldTimeMs <= 0)
            return;

        // Cancel any existing timer for this door
        CancelDoorHeldTimer(doorId);

        var cts = new CancellationTokenSource();
        _doorHeldTimers[doorId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(heldTimeMs, cts.Token);
                _logger.LogWarning("Door held open: '{DoorName}' (heldTime={HeldTimeMs}ms, extended={UseExtended})",
                    doorName, heldTimeMs, useExtended);
                _doorHeld[doorId] = true;
                _z9OpenCommunityProtocolService.SendDoorStateEvent(config, EvtCode.DoorHeld);
            }
            catch (TaskCanceledException)
            {
                // Timer cancelled — door closed in time
            }
        });
    }

    private void CancelDoorHeldTimer(int doorId)
    {
        if (_doorHeldTimers.TryGetValue(doorId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _doorHeldTimers.Remove(doorId);
        }
    }

    private void CancelAllDoorHeldTimers()
    {
        foreach (var cts in _doorHeldTimers.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _doorHeldTimers.Clear();
    }
}
