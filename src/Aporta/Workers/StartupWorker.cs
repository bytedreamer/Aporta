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

        await controlPoint.SetState(true);
        await Task.Delay(TimeSpan.FromSeconds(3));
        await controlPoint.SetState(false);

        _logger.LogInformation("Door strike deactivated for door unid={DoorUnid}", doorUnid);
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

        // Find which door has this endpoint as strike or contact
        var doors = await _doorRepository.GetAll();
        EvtCode? evtCode = null;

        var strikeDoor = doors.FirstOrDefault(d => d.DoorStrikeEndpointId == endpoint.Id);
        if (strikeDoor != null)
        {
            evtCode = state ? EvtCode.DoorUnlocked : EvtCode.DoorLocked;
        }

        var contactDoor = doors.FirstOrDefault(d => d.DoorContactEndpointId == endpoint.Id);
        if (contactDoor != null)
        {
            evtCode = state ? EvtCode.DoorClosed : EvtCode.DoorOpened;
        }

        if (evtCode == null)
            return;

        var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(driverEndpointId);
        if (config == null)
        {
            _logger.LogDebug("No Z9 config found for endpoint {EndpointId}, skipping door state event",
                driverEndpointId);
            return;
        }

        _z9OpenCommunityProtocolService.SendDoorStateEvent(config, evtCode.Value);
    }

    private void OnAccessDecisionMade(object sender, AccessDecisionEventArgs e)
    {
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
}