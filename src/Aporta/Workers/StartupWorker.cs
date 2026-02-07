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
    private readonly DoorRepository _doorRepository;
    private readonly EndpointRepository _endpointRepository;
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
        _doorRepository = new DoorRepository(dataAccess);
        _endpointRepository = new EndpointRepository(dataAccess);
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

            // Check if we're in Z9 Open Community mode BEFORE starting extensions
            // This prevents OSDP from auto-connecting using stale saved configuration
            var z9OpenCommunityHost = _configuration["z9OpenCommunityHost"];
            var isZ9OpenCommunityMode = !string.IsNullOrWhiteSpace(z9OpenCommunityHost);
            if (isZ9OpenCommunityMode)
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
        // Look up the endpoint by DriverEndpointId to get its database ID
        // The endpoint may not be persisted immediately, so retry a few times
        Aporta.Shared.Models.Endpoint readerEndpoint = null;
        for (int retry = 0; retry < 10; retry++)
        {
            var endpoints = await _endpointRepository.GetAll();
            readerEndpoint = endpoints.FirstOrDefault(ep => ep.DriverEndpointId == driverEndpointId);
            if (readerEndpoint != null)
                break;
            await Task.Delay(500);  // Wait for endpoint to be persisted
        }

        if (readerEndpoint == null)
        {
            _logger.LogWarning("Cannot create door: endpoint not found for DriverEndpointId {Id} after retries", driverEndpointId);
            return;
        }

        var doors = await _doorRepository.GetAll();

        // Check if any door already uses this reader as InAccess or OutAccess
        var existingDoor = doors.FirstOrDefault(d =>
            d.InAccessEndpointId == readerEndpoint.Id ||
            d.OutAccessEndpointId == readerEndpoint.Id);

        if (existingDoor != null)
        {
            _logger.LogDebug("Door '{DoorName}' already exists for reader endpoint {EndpointId}",
                existingDoor.Name, readerEndpoint.Id);
            return;
        }

        // Create a new door with this reader as the in-access point
        var door = new Aporta.Shared.Models.Door
        {
            Name = $"Door - {readerName}",
            InAccessEndpointId = readerEndpoint.Id,
        };

        var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);

        // If the reader has a strike output configured, wire it up
        if (config?.StrikeOutputNumber != null)
        {
            var strikeDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:O{config.StrikeOutputNumber}";
            _logger.LogInformation("Looking for strike output endpoint: {StrikeEndpointId}", strikeDriverEndpointId);

            Aporta.Shared.Models.Endpoint strikeEndpoint = null;
            for (int retry = 0; retry < 20; retry++)
            {
                var allEndpoints = await _endpointRepository.GetAll();
                strikeEndpoint = allEndpoints.FirstOrDefault(ep => ep.DriverEndpointId == strikeDriverEndpointId);
                if (strikeEndpoint != null)
                    break;
                await Task.Delay(500);
            }

            if (strikeEndpoint != null)
            {
                door.DoorStrikeEndpointId = strikeEndpoint.Id;
                _logger.LogInformation("Assigned door strike endpoint {StrikeEndpointId} to door '{DoorName}'",
                    strikeEndpoint.Id, door.Name);
            }
            else
            {
                _logger.LogWarning("Strike output endpoint {StrikeEndpointId} not found after retries", strikeDriverEndpointId);
            }
        }

        // Insert door now so access processing can proceed while we wire optional contact endpoint
        await _doorRepository.Insert(door);
        _logger.LogInformation("Created door '{DoorName}' for reader endpoint {EndpointId} (strike={StrikeId})",
            door.Name, readerEndpoint.Id, door.DoorStrikeEndpointId);

        // If the reader has a door contact input configured, wire it up (may take time if endpoint hasn't appeared yet)
        if (config?.DoorContactInputNumber != null)
        {
            var contactDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:I{config.DoorContactInputNumber}";
            _logger.LogInformation("Looking for door contact input endpoint: {ContactEndpointId}", contactDriverEndpointId);

            Aporta.Shared.Models.Endpoint contactEndpoint = null;
            for (int retry = 0; retry < 20; retry++)
            {
                var allEndpoints = await _endpointRepository.GetAll();
                contactEndpoint = allEndpoints.FirstOrDefault(ep => ep.DriverEndpointId == contactDriverEndpointId);
                if (contactEndpoint != null)
                    break;
                await Task.Delay(500);
            }

            if (contactEndpoint != null)
            {
                door.DoorContactEndpointId = contactEndpoint.Id;
                await _doorRepository.Update(door);
                _logger.LogInformation("Assigned door contact endpoint {ContactEndpointId} to door '{DoorName}'",
                    contactEndpoint.Id, door.Name);
            }
            else
            {
                _logger.LogWarning("Door contact input endpoint {ContactEndpointId} not found after retries", contactDriverEndpointId);
            }
        }

        // If the reader has a REX input configured, wire it up
        if (config?.RexInputNumber != null)
        {
            var rexDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:I{config.RexInputNumber}";
            _logger.LogInformation("Looking for REX input endpoint: {RexEndpointId}", rexDriverEndpointId);

            Aporta.Shared.Models.Endpoint rexEndpoint = null;
            for (int retry = 0; retry < 20; retry++)
            {
                var allEndpoints = await _endpointRepository.GetAll();
                rexEndpoint = allEndpoints.FirstOrDefault(ep => ep.DriverEndpointId == rexDriverEndpointId);
                if (rexEndpoint != null)
                    break;
                await Task.Delay(500);
            }

            if (rexEndpoint != null)
            {
                door.RequestToExitEndpointId = rexEndpoint.Id;
                await _doorRepository.Update(door);
                _logger.LogInformation("Assigned REX endpoint {RexEndpointId} to door '{DoorName}'",
                    rexEndpoint.Id, door.Name);
            }
            else
            {
                _logger.LogWarning("REX input endpoint {RexEndpointId} not found after retries", rexDriverEndpointId);
            }
        }

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

        var endpoints = await _endpointRepository.GetAll();
        var strikeEndpoint = endpoints.FirstOrDefault(ep => ep.DriverEndpointId == strikeDriverEndpointId);

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

        // Pin-based modes map to CardOnly for now (no pin support)
        var effectiveMode = doorModeType.Value;
        if (effectiveMode == DoorModeType.CardAndConfirmingPin ||
            effectiveMode == DoorModeType.UniquePinOnly ||
            effectiveMode == DoorModeType.CardOnlyOrUniquePin)
        {
            _logger.LogWarning("Door unid={DoorUnid}: pin-based mode {Mode} mapped to CardOnly (TODO: pin support)",
                doorUnid, effectiveMode);
            effectiveMode = DoorModeType.CardOnly;
        }

        var previousMode = _doorCurrentMode.TryGetValue(doorUnid, out var prev) ? prev : (DoorModeType?)null;
        _doorCurrentMode[doorUnid] = effectiveMode;

        _logger.LogInformation("Door unid={DoorUnid} mode changed to {Mode}", doorUnid, effectiveMode);

        // Actuate strike based on mode
        if (config.StrikeOutputNumber != null)
        {
            var strikeDriverEndpointId = $"{config.Host}:{config.TcpPort}:{config.OsdpAddress}:O{config.StrikeOutputNumber}";
            var endpoints = await _endpointRepository.GetAll();
            var strikeEndpoint = endpoints.FirstOrDefault(ep => ep.DriverEndpointId == strikeDriverEndpointId);

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
        // Look up the Aporta endpoint by driver endpoint ID
        var endpoints = await _endpointRepository.GetAll();
        var endpoint = endpoints.FirstOrDefault(ep => ep.DriverEndpointId == driverEndpointId);
        if (endpoint == null)
            return;

        var doors = await _doorRepository.GetAll();

        // Check if this is a REX input
        var rexDoor = doors.FirstOrDefault(d => d.RequestToExitEndpointId == endpoint.Id);
        if (rexDoor != null && state)
        {
            var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);
            if (config != null)
            {
                _logger.LogInformation("REX activated for door '{DoorName}'", rexDoor.Name);
                _z9OpenCommunityProtocolService.SendDoorStateEvent(config, EvtCode.ExitRequested);

                // Determine REX behavior based on door mode
                var currentMode = config.DoorUnid.HasValue && _doorCurrentMode.TryGetValue(config.DoorUnid.Value, out var mode)
                    ? mode : (DoorModeType?)null;

                if (currentMode == DoorModeType.StaticStateUnlocked)
                {
                    // Already unlocked — no need for momentary unlock
                }
                else if (currentMode == DoorModeType.StaticStateLocked)
                {
                    // Door is locked — do not unlock on REX
                    _logger.LogInformation("REX ignored for door '{DoorName}' — door is in locked mode", rexDoor.Name);
                }
                else if (config.ActivateStrikeOnRex && config.DoorUnid.HasValue)
                {
                    await HandleDoorMomentaryUnlock(config.DoorUnid.Value);
                }
            }
            return;
        }

        // Check strike endpoint — track state for forced-open detection
        var strikeDoor = doors.FirstOrDefault(d => d.DoorStrikeEndpointId == endpoint.Id);
        if (strikeDoor != null)
        {
            _doorStrikeActive[strikeDoor.Id] = state;
            var evtCode = state ? EvtCode.DoorUnlocked : EvtCode.DoorLocked;
            var config2 = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);
            if (config2 != null)
                _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, evtCode);
            return;
        }

        // Check contact endpoint — detect forced open
        var contactDoor = doors.FirstOrDefault(d => d.DoorContactEndpointId == endpoint.Id);
        if (contactDoor != null)
        {
            var config2 = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);
            if (config2 == null)
                return;

            if (!state) // Door opened
            {
                _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorOpened);

                _doorStrikeActive.TryGetValue(contactDoor.Id, out var strikeActive);
                if (!strikeActive)
                {
                    _logger.LogWarning("Door forced open: '{DoorName}'", contactDoor.Name);
                    _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorForced);
                    _doorForced[contactDoor.Id] = true;
                }

                StartDoorHeldTimer(contactDoor.Id, contactDoor.Name, config2);
            }
            else // Door closed
            {
                CancelDoorHeldTimer(contactDoor.Id);

                _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorClosed);

                if (_doorForced.TryGetValue(contactDoor.Id, out var wasForced) && wasForced)
                {
                    _logger.LogInformation("Door forced cleared: '{DoorName}'", contactDoor.Name);
                    _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorNotForced);
                    _doorForced[contactDoor.Id] = false;
                }

                if (_doorHeld.TryGetValue(contactDoor.Id, out var wasHeld) && wasHeld)
                {
                    _logger.LogInformation("Door held cleared: '{DoorName}'", contactDoor.Name);
                    _z9OpenCommunityProtocolService.SendDoorStateEvent(config2, EvtCode.DoorNotHeld);
                    _doorHeld[contactDoor.Id] = false;
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

        // Map EventReason to EvtSubCode (for granted, subCode is ignored so use default 0 value)
        var subCode = e.Reason switch
        {
            EventReason.CredentialNotEnrolled => EvtSubCode.AccessDeniedUnknownCredNum,
            EventReason.AccessNotAssigned => EvtSubCode.AccessDeniedNoPriv,
            EventReason.NoPrivilege => EvtSubCode.AccessDeniedNoPriv,
            EventReason.CredentialDisabled => EvtSubCode.AccessDeniedInactive,
            EventReason.CredentialNotYetEffective => EvtSubCode.AccessDeniedNotEffective,
            EventReason.CredentialExpired => EvtSubCode.AccessDeniedExpired,
            EventReason.OutsideSchedule => EvtSubCode.AccessDeniedOutsideSched,
            EventReason.DoorLocked => EvtSubCode.AccessDeniedNoPriv,
            EventReason.NoCredentialTemplate => EvtSubCode.AccessDeniedInactive,
            _ => EvtSubCode.AccessDeniedInactive  // Default value (0); unused for granted events
        };

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