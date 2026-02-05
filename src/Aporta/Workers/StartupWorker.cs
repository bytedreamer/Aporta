using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.Services;
using Aporta.Drivers.OSDP.Shared;
using Aporta.Drivers.OSDP.Shared.Actions;
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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Application is starting up services");
        try
        {
            await _dataAccess.UpdateSchema();
            await _extensionService.Startup();
            _accessService.Startup();

            var z9OpenCommunityHost = _configuration["z9OpenCommunityHost"];
            if (!string.IsNullOrWhiteSpace(z9OpenCommunityHost))
            {
                var z9OpenCommunityPort = int.TryParse(_configuration["z9OpenCommunityPort"], out var port)
                    ? port
                    : DefaultZ9OpenCommunityPort;
                var z9OpenCommunityId = _configuration["z9OpenCommunityId"];

                // Subscribe to OSDP configuration events before starting the service
                _z9OpenCommunityProtocolService.OsdpConfigurationReceived += OnOsdpConfigurationReceived;

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
            _z9OpenCommunityProtocolService.OsdpConfigurationReceived -= OnOsdpConfigurationReceived;
            _z9OpenCommunityProtocolService.Stop();
            _accessService.Shutdown();
            _extensionService.Shutdown();
        });
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
}