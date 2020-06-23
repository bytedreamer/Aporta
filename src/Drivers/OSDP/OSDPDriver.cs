using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Shared;

namespace Aporta.Drivers.OSDP
{
    /// <summary>
    /// 
    /// </summary>
    public class OSDPDriver : IHardwareDriver
    {
        private readonly List<IEndpoint> _endpoints = new List<IEndpoint>();
        private readonly Dictionary<string, Guid> _portMapping = new Dictionary<string, Guid>();
        
        private  ControlPanel _panel;
        private ILogger<OSDPDriver> _logger;
        private Settings _settings = new Settings{Buses = new Bus[]{}};

        public Guid Id => Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5");

        public string Name => "OSDP";

        public IEnumerable<IDevice> Devices => new List<IDevice>();

        public IEnumerable<IEndpoint> Endpoints => _endpoints;

        public void Load(string settings, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<OSDPDriver>();
            _panel = new ControlPanel(loggerFactory.CreateLogger<ControlPanel>());
            
            ExtractSettings(settings);

            StartConnections();

            AddDevices();
        }

        private void AddDevices()
        {
            foreach (var bus in _settings.Buses)
            {

            }

            if (_portMapping.Any())
            {
                var connection = _portMapping.First().Value;
                _panel.AddDevice(connection, 1, true, false);

                _endpoints.Add(new OSDPControlPoint(_panel, connection, 1, 0));
            }
        }

        private void StartConnections()
        {
            foreach (var bus in _settings.Buses)
            {
                _portMapping.Add(bus.PortName,
                    _panel.StartConnection(new SerialPortOsdpConnection(bus.PortName, bus.BaudRate)));
            }
        }

        private void ExtractSettings(string settings)
        {
            if (!string.IsNullOrWhiteSpace(settings))
            {
                try
                {
                    _settings = JsonSerializer.Deserialize<Settings>(settings);
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, $"Unable to deserialize {settings}");
                    _settings = new Settings{Buses = new Bus[]{}};
                }
            }

            _settings.AvailablePorts = SerialPort.GetPortNames();
        }

        public void Unload()
        {
            _panel.Shutdown();
        }

        public string InitialSettings()
        {
            return JsonSerializer.Serialize(_settings);
        }
    }
}