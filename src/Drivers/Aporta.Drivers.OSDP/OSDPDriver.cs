using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using Aporta.Drivers.OSDP.Shared;
using Aporta.Drivers.OSDP.Shared.Actions;
using Newtonsoft.Json;
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
        private readonly List<IEndpoint> _endpoints = new List<IEndpoint>();

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
            _panel.InputStatusReportReplyReceived += PanelOnInputStatusReportReplyReceived;
            _panel.OutputStatusReportReplyReceived += PanelOnOutputStatusReportReplyReceived;

            ExtractConfiguration(configuration);
            
            StartConnections();
            
            // need to do before adding devices, add known endpoints before devices come online
            AddEndpoints();

            AddDevices();
        }

        private void AddEndpoints()
        {
            foreach (var bus in _configuration.Buses)
            {
                foreach (var device in bus.Devices)
                {
                    foreach (var input in device.Inputs)
                    {
                        _endpoints.Add(new OSDPMonitorPoint(Id, _panel, _portMapping[bus.PortName], device,
                            input));
                    }
                    
                    foreach (var output in device.Outputs)
                    {
                        _endpoints.Add(new OSDPControlPoint(Id, _panel, _portMapping[bus.PortName], device,
                            output));
                    }

                    foreach (var reader in device.Readers)
                    {
                        _endpoints.Add(new OSDPAccessPoint(Id, _panel, _portMapping[bus.PortName], device,
                            reader));
                    }
                }
            }
        }

        public IEnumerable<IEndpoint> Endpoints => _endpoints;

        private async void PanelOnConnectionStatusChanged(object sender, ControlPanel.ConnectionStatusEventArgs eventArgs)
        {
            var matchingBus = _configuration.Buses.Single(bus =>
                bus.PortName == _portMapping.First(keyValue => keyValue.Value == eventArgs.ConnectionId).Key);
            var matchingDevice = matchingBus.Devices.First(device => device.Address == eventArgs.Address);

            if (!eventArgs.IsConnected || matchingDevice.CheckedCapabilities)
            {
                matchingDevice.IsConnected = eventArgs.IsConnected;
                
                OnUpdatedEndpoints();
                return;
            }
            
            DeviceCapabilities capabilities;
            try
            {
                capabilities = await _panel.DeviceCapabilities(eventArgs.ConnectionId, eventArgs.Address);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to get device capabilities");
                return;
            }
            
            foreach (var inputCapability in capabilities.Capabilities.Where(capability =>
                capability.Function == CapabilityFunction.ContactStatusMonitoring))
            {
                if (matchingDevice.Outputs.Any()) continue;

                for (byte inputNumber = 0; inputNumber < inputCapability.NumberOf; inputNumber++)
                {
                    var input = new Input
                    {
                        Name = $"{matchingDevice.Name} [Input {inputNumber}]",
                        Number = inputNumber
                    };
                    matchingDevice.Inputs.Add(input);
                    _endpoints.Add(new OSDPMonitorPoint(Id, _panel, eventArgs.ConnectionId, matchingDevice,
                        input));
                }
            }

            foreach (var outputCapability in capabilities.Capabilities.Where(capability =>
                capability.Function == CapabilityFunction.OutputControl))
            {
                if (matchingDevice.Outputs.Any()) continue;

                for (byte outputNumber = 0; outputNumber < outputCapability.NumberOf; outputNumber++)
                {
                    var output = new Output
                    {
                        Name = $"{matchingDevice.Name} [Output {outputNumber}]",
                        Number = outputNumber
                    };
                    matchingDevice.Outputs.Add(output);
                    _endpoints.Add(new OSDPControlPoint(Id, _panel, eventArgs.ConnectionId, matchingDevice,
                        output));
                }
            }

            // Some readers don't report Reader capability
            if (capabilities.Capabilities.Any(capability => capability.Function == CapabilityFunction.CardDataFormat))
            {
                const byte readerNumber = 0;
                var reader = new Reader
                {
                    Name = $"{matchingDevice.Name} [Reader {readerNumber}]",
                    Number = readerNumber
                };
                matchingDevice.Readers.Add(reader);
                _endpoints.Add(new OSDPAccessPoint(Id, _panel, eventArgs.ConnectionId, matchingDevice,
                    reader));
            }
            
            foreach (var readerCapability in capabilities.Capabilities.Where(capability =>
                capability.Function == CapabilityFunction.Readers))
            {
                if (matchingDevice.Readers.Any()) continue;

                for (byte readerNumber = 1; readerNumber < readerCapability.NumberOf; readerNumber++)
                {
                    var reader = new Reader
                    {
                        Name = $"{matchingDevice.Name} [Reader {readerNumber}]",
                        Number = readerNumber
                    };
                    matchingDevice.Readers.Add(reader);
                    _endpoints.Add(new OSDPAccessPoint(Id, _panel, eventArgs.ConnectionId, matchingDevice,
                        reader));
                }
            }

            matchingDevice.CheckedCapabilities = true;
            matchingDevice.IsConnected = eventArgs.IsConnected;
                
            OnUpdatedEndpoints();
        }

        private void PanelOnRawCardDataReplyReceived(object sender, ControlPanel.RawCardDataReplyEventArgs eventArgs)
        {
            var accessPoints = _endpoints.Where(endpoint => endpoint is IAccessPoint).Cast<IAccessPoint>()
                .Where(accessPoint => accessPoint.Id.Split(":").First() == eventArgs.Address.ToString());
            foreach (var accessPoint in accessPoints)
            {
                AccessCredentialReceived?.Invoke(this,
                    new AccessCredentialReceivedEventArgs(
                        accessPoint, eventArgs.RawCardData.Data, eventArgs.RawCardData.BitCount));
            }
        }

        private void PanelOnInputStatusReportReplyReceived(object sender,
            ControlPanel.InputStatusReportReplyEventArgs eventArgs)
        {
            var monitorPoints = _endpoints.Where(endpoint => endpoint is IMonitorPoint).Cast<IMonitorPoint>()
                .Where(monitorPoint => monitorPoint.Id.Split(":").First() == eventArgs.Address.ToString());

            foreach (var monitorPoint in monitorPoints)
            {
                StateChanged?.Invoke(this,
                    new StateChangedEventArgs(monitorPoint, eventArgs.InputStatus.InputStatuses.ToArray()[
                            short.Parse(monitorPoint.Id.Split(":").Last().TrimStart('I'))]));
            }
        }

        private void PanelOnOutputStatusReportReplyReceived(object sender,
            ControlPanel.OutputStatusReportReplyEventArgs eventArgs)
        {
            var controlPoints = _endpoints.Where(endpoint => endpoint is IControlPoint).Cast<IControlPoint>()
                .Where(controlPoint => controlPoint.Id.Split(":").First() == eventArgs.Address.ToString());

            foreach (var controlPoint in controlPoints)
            {
                StateChanged?.Invoke(this,
                    new StateChangedEventArgs(controlPoint, eventArgs.OutputStatus.OutputStatuses.ToArray()[
                            short.Parse(controlPoint.Id.Split(":").Last().TrimStart('O'))]));
            }
        }

        private void AddDevices()
        {
            foreach (var bus in _configuration.Buses)
            {
                var connection = _portMapping[bus.PortName];
                foreach (var device in bus.Devices)
                {
                    _panel.AddDevice(connection, device.Address, true, device.RequireSecurity);
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
                    _configuration = JsonConvert.DeserializeObject<Configuration>(configuration);
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
            _panel.InputStatusReportReplyReceived -= PanelOnInputStatusReportReplyReceived;
            _panel.OutputStatusReportReplyReceived -= PanelOnOutputStatusReportReplyReceived;
        }

        public string CurrentConfiguration()
        {
            return JsonConvert.SerializeObject(_configuration);
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

        public event EventHandler<EventArgs> UpdatedEndpoints;
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;
        public event EventHandler<StateChangedEventArgs> StateChanged;
        public event EventHandler<OnlineStatusChangedEventArgs> OnlineStatusChanged;

        private string AddBus(string parameters)
        {
            var busAction = JsonConvert.DeserializeObject<BusAction>(parameters);

            _configuration.Buses.Add(busAction.Bus);

            if (!_portMapping.ContainsKey(busAction.Bus.PortName))
            {
                _portMapping.Add(busAction.Bus.PortName,
                    _panel.StartConnection(new SerialPortOsdpConnection(busAction.Bus.PortName, busAction.Bus.BaudRate)));
            }

            return string.Empty;
        }
        
        private string RemoveBus(string parameters)
        {
            var busAction = JsonConvert.DeserializeObject<BusAction>(parameters);

            foreach (var device in busAction.Bus.Devices)
            {
                _endpoints.RemoveAll(endpoint => endpoint.Id.Split(':').First() == device.Address.ToString());
            }
            OnUpdatedEndpoints();

            var devices = _configuration.Buses.First(bus => bus.PortName == busAction.Bus.PortName).Devices;
            var addresses = devices.Select(device => device.Address).ToArray();
            devices.Clear();
            foreach (byte address in addresses)
            {
                _panel.RemoveDevice(_portMapping[busAction.Bus.PortName], address);
            }

            _configuration.Buses.RemoveAll(bus => bus.PortName == busAction.Bus.PortName);

            return string.Empty;
        }

        private string AddUpdateDevice(string parameters)
        {
            var deviceAction = JsonConvert.DeserializeObject<DeviceAction>(parameters);

            _configuration.Buses.First(bus => bus.PortName == deviceAction.PortName).Devices.Add(deviceAction.Device);
            
            _panel.AddDevice(_portMapping[deviceAction.PortName], deviceAction.Device.Address, true, false);
            
            return string.Empty;
        }

        private string RemoveDevice(string parameters)
        {
            var deviceAction = JsonConvert.DeserializeObject<DeviceAction>(parameters);

            _endpoints.RemoveAll(endpoint => endpoint.Id.Split(':').First() == deviceAction.Device.Address.ToString());
            OnUpdatedEndpoints();
            
            _configuration.Buses.First(bus => bus.PortName == deviceAction.PortName).Devices
                .RemoveAll(device => device.Address == deviceAction.Device.Address);

            _panel.RemoveDevice(_portMapping[deviceAction.PortName], deviceAction.Device.Address);

            return string.Empty;
        }

        protected virtual void OnUpdatedEndpoints()
        {
            UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
        }
    }
}