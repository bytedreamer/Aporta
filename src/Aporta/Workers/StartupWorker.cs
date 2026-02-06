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

                // Subscribe to OSDP configuration events before starting the service
                _z9OpenCommunityProtocolService.OsdpConfigurationReceived += OnOsdpConfigurationReceived;

                // Subscribe to device online status changes to forward via community protocol
                _extensionService.OnlineStatusChanged += OnOnlineStatusChanged;

                // Subscribe to access decisions to forward via community protocol
                _accessService.AccessDecisionMade += OnAccessDecisionMade;

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
        // Get the Z9 config for this endpoint to send event with correct device unid
        var config = _z9OpenCommunityProtocolService.GetConfigForEndpoint(e.Endpoint?.Id);
        if (config == null)
        {
            _logger.LogDebug("No Z9 config found for endpoint {EndpointId}, skipping event",
                e.Endpoint?.Id);
            return;
        }

        _logger.LogInformation("OSDP reader {Name} is now {Status}",
            config.Name, e.IsOnline ? "online" : "offline");

        // When reader comes online, ensure a Door exists for access control decisions
        if (e.IsOnline && e.Endpoint != null)
        {
            var driverEndpointId = e.Endpoint.Id;  // This is the DriverEndpointId (string)
            Task.Run(async () =>
            {
                try
                {
                    await EnsureDoorExistsForReader(driverEndpointId, config.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create door for reader {Name}", config.Name);
                }
            });
        }

        _z9OpenCommunityProtocolService.SendCredReaderOnlineEvent(config, e.IsOnline);
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
            // Note: DoorStrikeEndpointId is left null - access decisions will still be made
            // but the strike won't be activated (appropriate for simple test scenario)
        };

        await _doorRepository.Insert(door);
        _logger.LogInformation("Created door '{DoorName}' for reader endpoint {EndpointId}",
            door.Name, readerEndpoint.Id);
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