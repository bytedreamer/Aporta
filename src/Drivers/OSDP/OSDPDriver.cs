using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using OSDP.Drivers.Shared;
using OSDP.Drivers.Shared.Actions;
using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Net.Model.ReplyData;

namespace Aporta.Drivers.OSDP
{
    /// <summary>
    /// 
    /// </summary>
    public class OSDPDriver : IHardwareDriver
    {
        private readonly Dictionary<string, Guid> _portMapping = new Dictionary<string, Guid>();

        private ControlPanel _panel;
        private ILogger<OSDPDriver> _logger;
        private Configuration _configuration = new Configuration {Buses = new List<Bus>()};

        public Guid Id => Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5");

        public string Name => "OSDP";

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<OSDPDriver>();
            _panel = new ControlPanel(loggerFactory.CreateLogger<ControlPanel>());
            
            _panel.ConnectionStatusChanged += PanelOnConnectionStatusChanged;
            _panel.RawCardDataReplyReceived += PanelOnRawCardDataReplyReceived;

            ExtractConfiguration(configuration);
            
            StartConnections();

            AddDevices();
        }
        
        private async void PanelOnConnectionStatusChanged(object sender, ControlPanel.ConnectionStatusEventArgs eventArgs)
        {
            var matchingBus = _configuration.Buses.Single(bus =>
                bus.PortName == _portMapping.First(keyValue => keyValue.Value == eventArgs.ConnectionId).Key);
            var matchingDevice = matchingBus.Devices.First(device => device.Address == eventArgs.Address);

            List<IEndpoint> endpoints = new List<IEndpoint>();
            
            if (eventArgs.IsConnected && !matchingDevice.CheckedCapabilities)
            {
                var capabilities = await _panel.DeviceCapabilities(eventArgs.ConnectionId, eventArgs.Address);
                foreach (var outputCapability in capabilities.Capabilities.Where(capability =>
                    capability.Function == CapabilityFunction.OutputControl))
                {
                    if (matchingDevice.Outputs.Any()) continue;

                    for (byte outputNumber = 0; outputNumber < outputCapability.NumberOf; outputNumber++)
                    {
                        var output = new Output
                        {
                            Name = $"{matchingDevice.Name} - Output {outputNumber}",
                            Number = outputNumber
                        };
                        matchingDevice.Outputs.Add(output);
                        endpoints.Add(new OSDPControlPoint(Id, _panel, eventArgs.ConnectionId, matchingDevice,
                            output));
                    }
                }

                foreach (var readerCapability in capabilities.Capabilities.Where(capability =>
                    capability.Function == CapabilityFunction.Readers))
                {
                    if (matchingDevice.Readers.Any()) continue;

                    for (byte readerNumber = 0; readerNumber < readerCapability.NumberOf; readerNumber++)
                    {
                        var reader = new Reader
                        {
                            Name = $"{matchingDevice.Name} - Reader {readerNumber}",
                            Number = readerNumber
                        };
                        matchingDevice.Readers.Add(reader);
                        endpoints.Add(new OSDPAccessPoint(Id, _panel, eventArgs.ConnectionId, matchingDevice,
                            reader));
                    }
                }

                matchingDevice.CheckedCapabilities = true;
                
                OnAddEndpoints(endpoints);
            }
            else if (eventArgs.IsConnected && matchingDevice.CheckedCapabilities)
            {
                endpoints.AddRange(matchingDevice.Readers.Select(matchingDeviceReader =>
                    new OSDPAccessPoint(Id, _panel, eventArgs.ConnectionId, matchingDevice,
                        matchingDeviceReader)));
                endpoints.AddRange(matchingDevice.Outputs.Select(matchingDeviceOutput =>
                    new OSDPControlPoint(Id, _panel, eventArgs.ConnectionId, matchingDevice, matchingDeviceOutput)));
                
                OnAddEndpoints(endpoints);
            }
        }
        
        private void PanelOnRawCardDataReplyReceived(object sender, ControlPanel.RawCardDataReplyEventArgs eventArgs)
        {

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
            
            _panel.ConnectionStatusChanged -= PanelOnConnectionStatusChanged;
            _panel.RawCardDataReplyReceived -= PanelOnRawCardDataReplyReceived;
        }

        public string CurrentConfiguration()
        {
            return JsonSerializer.Serialize(_configuration);
        }

        public async Task<string> PerformAction(string action, string parameters)
        {
            Enum.TryParse(action, out ActionType actionEnum);
            return actionEnum switch
            {
                ActionType.AddBus => AddBus(parameters),
                ActionType.RemoveBus => RemoveBus(parameters),
                ActionType.AddUpdateDevice => AddUpdateDevice(parameters),
                ActionType.RemoveDevice => RemoveDevice(parameters),
                _ => await Task.FromResult(string.Empty)
            };
        }

        public event EventHandler<AddEndpointsEventArgs> AddEndpoints;
        
        private string AddBus(string parameters)
        {
            var busAction = JsonSerializer.Deserialize<BusAction>(parameters);

            _configuration.Buses.Add(busAction.Bus);

            _portMapping.Add(busAction.Bus.PortName,
                _panel.StartConnection(new SerialPortOsdpConnection(busAction.Bus.PortName, busAction.Bus.BaudRate)));
            
            return string.Empty;
        }
        
        private string RemoveBus(string parameters)
        {
            var busAction = JsonSerializer.Deserialize<BusAction>(parameters);

            var devices = _configuration.Buses.First(bus => bus.PortName == busAction.Bus.PortName).Devices;
            var addresses = devices.Select(device => device.Address).ToArray();
            devices.Clear();
            foreach (byte address in addresses)
            {
                _panel.RemoveDevice(_portMapping[busAction.Bus.PortName], address);
            }

            _configuration.Buses.RemoveAll(bus => bus.PortName == busAction.Bus.PortName);

            _portMapping.Remove(busAction.Bus.PortName);

            return string.Empty;
        }

        private string AddUpdateDevice(string parameters)
        {
            var deviceAction = JsonSerializer.Deserialize<DeviceAction>(parameters);

            _configuration.Buses.First(bus => bus.PortName == deviceAction.PortName).Devices.Add(deviceAction.Device);
            
            _panel.AddDevice(_portMapping[deviceAction.PortName], deviceAction.Device.Address, true, false);
            
            return string.Empty;
        }

        private string RemoveDevice(string parameters)
        {
            var deviceAction = JsonSerializer.Deserialize<DeviceAction>(parameters);
            
            _configuration.Buses.First(bus => bus.PortName == deviceAction.PortName).Devices
                .RemoveAll(device => device.Address == deviceAction.Device.Address);

            _panel.RemoveDevice(_portMapping[deviceAction.PortName], deviceAction.Device.Address);

            return string.Empty;
        }

        protected virtual void OnAddEndpoints(IEnumerable<IEndpoint> endpoints)
        {
            AddEndpoints?.Invoke(this, new AddEndpointsEventArgs{Endpoints = endpoints});
        }
    }
}