using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using OSDP.Drivers.Shared.Actions;
using OSDP.Net;
using OSDP.Net.Connections;

namespace Aporta.Drivers.OSDP
{
    /// <summary>
    /// 
    /// </summary>
    public class OSDPDriver : IHardwareDriver
    {
        private readonly List<IEndpoint> _endpoints = new List<IEndpoint>();
        private readonly Dictionary<string, Guid> _portMapping = new Dictionary<string, Guid>();

        private ControlPanel _panel;
        private ILogger<OSDPDriver> _logger;
        private Configuration _configuration = new Configuration {Buses = new List<Bus>()};

        public Guid Id => Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5");

        public string Name => "OSDP";

        public IEnumerable<IEndpoint> Endpoints => _endpoints;

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<OSDPDriver>();
            _panel = new ControlPanel(loggerFactory.CreateLogger<ControlPanel>());

            ExtractConfiguration(configuration);

            StartConnections();

            AddDevices();
        }

        private void AddDevices()
        {
            foreach (var bus in _configuration.Buses)
            {
                var connection = _portMapping[bus.PortName];
                foreach (var device in bus.Devices)
                {
                    _panel.AddDevice(connection, device.Address, true, false);
                }

                //_endpoints.Add(new OSDPControlPoint(_panel, connection, 1, 0));
            }
        }

        private void StartConnections()
        {
            foreach (var bus in _configuration.Buses)
            {
                _portMapping.Add(bus.PortName,
                    _panel.StartConnection(new SerialPortOsdpConnection(bus.PortName, bus.BaudRate)));
            }
        }

        private void ExtractConfiguration(string configuration)
        {
            if (!string.IsNullOrWhiteSpace(configuration))
            {
                try
                {
                    _configuration = JsonSerializer.Deserialize<Configuration>(configuration);
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, $"Unable to deserialize {configuration}");
                    _configuration = new Configuration {Buses = new List<Bus>()};
                }
            }

            _configuration.AvailablePorts = SerialPort.GetPortNames();
        }

        public void Unload()
        {
            _panel.Shutdown();
        }

        public string InitialConfiguration()
        {
            return JsonSerializer.Serialize(_configuration);
        }

        public async Task<string> PerformAction(string action, string parameters)
        {
            Enum.TryParse(action, out ActionType actionEnum);
            return actionEnum switch
            {
                ActionType.AddUpdateDevice => AddUpdateDevice(parameters),
                ActionType.RemoveDevice => RemoveDevice(parameters),
                _ => await Task.FromResult(string.Empty)
            };
        }

        private string AddUpdateDevice(string parameters)
        {
            var deviceAction = JsonSerializer.Deserialize<DeviceAction>(parameters);
            
            _panel.AddDevice(_portMapping[deviceAction.PortName], deviceAction.Device.Address, true, false);

            return string.Empty;
        }
        
        private string RemoveDevice(string parameters)
        {
            var deviceAction = JsonSerializer.Deserialize<DeviceAction>(parameters);
            
            _panel.RemoveDevice(_portMapping[deviceAction.PortName], deviceAction.Device.Address);

            return string.Empty;
        }
    }
}