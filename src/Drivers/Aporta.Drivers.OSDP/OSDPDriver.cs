using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using OSDP.Net;
using OSDP.Net.Connections;
using OSDP.Net.Model.ReplyData;

using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Aporta.Drivers.OSDP.Shared;
using Aporta.Drivers.OSDP.Shared.Actions;
using Aporta.Extensions;
using OSDP.Net.Model.CommandData;
using PKOC.Net;
using ErrorCode = OSDP.Net.Model.ReplyData.ErrorCode;

namespace Aporta.Drivers.OSDP;

/// <summary>
/// 
/// </summary>
public class OSDPDriver : IHardwareDriver
{
    private readonly ConcurrentDictionary<string, Guid> _portMapping = new();
    private readonly List<IEndpoint> _endpoints = new();
    private readonly ConcurrentDictionary<int, DeviceSettings> _pkocDevices = new();
        
    private ControlPanel _panel;
    private PKOCControlPanel _pkocPanel;
    private IDataEncryption _dataEncryption;
    private ILogger<OSDPDriver> _logger;
    private Configuration _configuration = new() {Buses = new List<Bus>()};

    public Guid Id => Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5");

    /// <inheritdoc />
    public string Name => "OSDP";

    /// <inheritdoc />
    public void Load(string configuration, IDataEncryption dataEncryption, ILoggerFactory loggerFactory)
    {
        _dataEncryption = dataEncryption;
        _logger = loggerFactory.CreateLogger<OSDPDriver>();
        _panel = new ControlPanel(loggerFactory.CreateLogger<ControlPanel>());
        _pkocPanel = new PKOCControlPanel(_panel);
            
        _panel.ConnectionStatusChanged += PanelOnConnectionStatusChanged;
        _panel.RawCardDataReplyReceived += PanelOnRawCardDataReplyReceived;
        _panel.InputStatusReportReplyReceived += PanelOnInputStatusReportReplyReceived;
        _panel.OutputStatusReportReplyReceived += PanelOnOutputStatusReportReplyReceived;
        _panel.NakReplyReceived += PanelOnNakReplyReceived;
        
        _pkocPanel.CardPresented += PkocPanelOnCardPresented;

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
                    _endpoints.Add(new OSDPInput(Id, _panel, _portMapping[bus.PortName], device,
                        input));
                }

                foreach (var output in device.Outputs)
                {
                    _endpoints.Add(new OSDPOutput(Id, _panel, _portMapping[bus.PortName], device,
                        output));
                }

                foreach (var reader in device.Readers)
                {
                    _endpoints.Add(new OSDPAccess(Id, device, reader, _panel, _portMapping[bus.PortName]));
                }
            }
        }
    }

    public IEnumerable<IEndpoint> Endpoints => _endpoints;

    private void PanelOnConnectionStatusChanged(object sender, ControlPanel.ConnectionStatusEventArgs eventArgs)
    {
        Task.Run(async () =>
        {
            var matchingBus = _configuration.Buses.Single(bus =>
                bus.PortName == _portMapping.First(keyValue => keyValue.Value == eventArgs.ConnectionId).Key);
            var matchingDevice = matchingBus.Devices.First(device => device.Address == eventArgs.Address);

            matchingDevice.KeyMismatch = false;

            switch (eventArgs.IsConnected)
            {
                case false:
                    _logger.LogWarning("Device \'{MatchingDeviceName}\' is offline", matchingDevice.Name);
                    matchingDevice.IsConnected = false;
                    matchingDevice.IdentityNotMatched = false;

                    OnUpdatedEndpoints();
                    return;
                case true:
                {
                    _logger.LogInformation("Device \'{MatchingDeviceName}\' is online", matchingDevice.Name);

                    if (matchingDevice.SecureMode == SecureMode.Install)
                    {
                        _logger.LogInformation(
                            "Device \'{MatchingDeviceName}\' is in install mode, attempting to rotate key",
                            matchingDevice.Name);
                        await RotateKey(JsonConvert.SerializeObject(new DeviceAction { Device = matchingDevice }));

                        matchingDevice.IsConnected = true;
                        OnUpdatedEndpoints();
                        return;
                    }

                    if (!await ProcessDeviceIdentification(eventArgs, matchingDevice).ConfigureAwait(false))
                    {
                        matchingDevice.IsConnected = false;
                        OnUpdatedEndpoints();
                        return;
                    }

                    if (!await ProcessDeviceCapabilities(eventArgs, matchingDevice).ConfigureAwait(false))
                    {
                        matchingDevice.IsConnected = false;
                        OnUpdatedEndpoints();
                        return;
                    }

                    matchingDevice.IsConnected = true;
                    OnUpdatedEndpoints();

                    if (matchingDevice.PKOCEnabled)
                    {
                        var deviceSettings = new DeviceSettings(eventArgs.ConnectionId, eventArgs.Address);
                        bool successfulInitialization = await _pkocPanel.InitializePKOC(deviceSettings);
                        if (successfulInitialization)
                        {
                            _logger.LogInformation("The OSDP reader has been successfully initialized for PKOC");
                            _pkocDevices.TryAdd(deviceSettings.GetHashCode(), deviceSettings);
                        }
                        else
                        {
                            _logger.LogWarning("The OSDP reader has not been successfully initialized for PKOC");
                        }
                    }

                    return;
                }
            }
        });
    }

    private async Task<bool> ProcessDeviceCapabilities(ControlPanel.ConnectionStatusEventArgs eventArgs, Device matchingDevice)
    {
        DeviceCapabilities capabilities;
        try
        {
            capabilities = await _panel.DeviceCapabilities(eventArgs.ConnectionId, eventArgs.Address).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to get device capabilities for \'{MatchingDeviceName}\'",
                matchingDevice.Name);
            _panel.RemoveDevice(eventArgs.ConnectionId, eventArgs.Address);
            return false;
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
                _endpoints.Add(new OSDPInput(Id, _panel, eventArgs.ConnectionId, matchingDevice,
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
                _endpoints.Add(new OSDPOutput(Id, _panel, eventArgs.ConnectionId, matchingDevice,
                    output));
            }
        }

        // Some readers don't report Reader capability
        if (capabilities.Capabilities.Any(capability => capability.Function == CapabilityFunction.CardDataFormat) &&
            !matchingDevice.Readers.Any())
        {
            const byte readerNumber = 0;
            var reader = new Reader
            {
                Name = $"{matchingDevice.Name} [Reader {readerNumber}]",
                Number = readerNumber
            };
            matchingDevice.Readers.Add(reader);
            _endpoints.Add(new OSDPAccess(Id, matchingDevice, reader, _panel, eventArgs.ConnectionId));
        }

        matchingDevice.CheckedCapabilities = true;

        return true;
    }

    private async Task<bool> ProcessDeviceIdentification(ControlPanel.ConnectionStatusEventArgs eventArgs, Device matchingDevice)
    {
        DeviceIdentification identification;
        try
        {
            identification = await _panel.IdReport(eventArgs.ConnectionId, eventArgs.Address).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to get device identification for \'{MatchingDeviceName}\'",
                matchingDevice.Name);
            _panel.RemoveDevice(eventArgs.ConnectionId, eventArgs.Address);
            return false;
        }

        if (matchingDevice.Identification == null)
        {
            matchingDevice.Identification = new Identification
            {
                VendorCode = BitConverter.ToString(identification.VendorCode.ToArray()),
                SerialNumber = identification.SerialNumber,
                FirmwareVersion =
                    $"{identification.FirmwareMajor}.{identification.FirmwareMinor}.{identification.FirmwareBuild}"
            };
        }
        else
        {
            if (matchingDevice.Identification.VendorCode != BitConverter.ToString(identification.VendorCode.ToArray()) ||
                matchingDevice.Identification.SerialNumber != identification.SerialNumber)
            {
                matchingDevice.IdentityNotMatched = true;
                _logger.LogWarning("Device identification mismatch for \'{MatchingDeviceName}\'", matchingDevice.Name);
                _panel.RemoveDevice(eventArgs.ConnectionId, eventArgs.Address);
                return false;
            }
        }

        matchingDevice.IdentityNotMatched = false;
        
        return true;
    }

    private void PanelOnRawCardDataReplyReceived(object sender, ControlPanel.RawCardDataReplyEventArgs eventArgs)
    {
        Task.Run(() =>
        {
            var accessPoint = _endpoints.Where(endpoint => endpoint is IAccess).Cast<IAccess>()
                .SingleOrDefault(accessPoint =>
                    eventArgs.ConnectionId == _portMapping[accessPoint.Id.Split(":")[0]] &&
                    accessPoint.Id.Split(":")[1] == eventArgs.Address.ToString());
            if (accessPoint != null)
            {
                AccessCredentialReceived?.Invoke(this,
                    new AccessCredentialReceivedEventArgs(
                        accessPoint,
                        new WiegandCredentialHandler(eventArgs.RawCardData.Data, eventArgs.RawCardData.BitCount,
                            accessPoint.Name, _logger)));
            }
            else
            {
                _logger.LogWarning("Unable to find access point at address {EventArgsAddress} to process card read",
                    eventArgs.Address);
            }
        });
    }

    private void PkocPanelOnCardPresented(object sender, CardPresentedEventArgs eventArgs)
    {
        Task.Run(async () =>
        {
            _logger.LogInformation("A PKOC card has been presented to the reader");
            var accessPoint = _endpoints.Where(endpoint => endpoint is IAccess).Cast<IAccess>()
                .SingleOrDefault(accessPoint =>
                    eventArgs.ConnectionId == _portMapping[accessPoint.Id.Split(":")[0]] &&
                    accessPoint.Id.Split(":")[1] == eventArgs.Address.ToString());
            if (accessPoint != null)
            {
                var hashLookup = new DeviceSettings(eventArgs.ConnectionId, eventArgs.Address).GetHashCode();
                var deviceSettings = _pkocDevices[hashLookup];
                var result = await _pkocPanel.AuthenticationRequest(deviceSettings);

                AccessCredentialReceived?.Invoke(this,
                    new AccessCredentialReceivedEventArgs(
                        accessPoint, new PKOCCredentialHandler(result, accessPoint.Name, _logger)));

            }
            else
            {
                _logger.LogWarning("Unable to find access point at address {EventArgsAddress} to process card read",
                    eventArgs.Address);
            }
        });
    }

    private void PanelOnInputStatusReportReplyReceived(object sender,
        ControlPanel.InputStatusReportReplyEventArgs eventArgs)
    {
        Task.Run(() =>
        {        
            var monitorPoints = _endpoints.Where(endpoint => endpoint is IInput).Cast<IInput>()
                .Where(monitorPoint =>
                    eventArgs.ConnectionId == _portMapping[monitorPoint.Id.Split(":")[0]] &&
                    monitorPoint.Id.Split(":")[1] == eventArgs.Address.ToString());

            foreach (var monitorPoint in monitorPoints)
            {
                StateChanged?.Invoke(this,
                    new StateChangedEventArgs(monitorPoint, eventArgs.InputStatus.InputStatuses.ToArray()[
                        short.Parse(monitorPoint.Id.Split(":").Last().TrimStart('I'))]));
            }
        });
    }

    private void PanelOnOutputStatusReportReplyReceived(object sender,
        ControlPanel.OutputStatusReportReplyEventArgs eventArgs)
    {
        Task.Run(() =>
        {
            var controlPoints = _endpoints.Where(endpoint => endpoint is IOutput).Cast<IOutput>()
                .Where(controlPoint =>
                    eventArgs.ConnectionId == _portMapping[controlPoint.Id.Split(":")[0]] &&
                    controlPoint.Id.Split(":")[1] == eventArgs.Address.ToString());

            foreach (var controlPoint in controlPoints)
            {
                StateChanged?.Invoke(this,
                    new StateChangedEventArgs(controlPoint, eventArgs.OutputStatus.OutputStatuses.ToArray()[
                        short.Parse(controlPoint.Id.Split(":").Last().TrimStart('O'))]));
            }
        });
    }
    
    private void PanelOnNakReplyReceived(object sender, ControlPanel.NakReplyEventArgs eventArgs)
    {
        Task.Run(() =>
        {
            if (eventArgs.Nak.ErrorCode != ErrorCode.CommunicationSecurityNotMet) return;

            var device = _configuration.Buses.First(bus => _portMapping[bus.PortName] == eventArgs.ConnectionId).Devices
                .First(device => device.Address == eventArgs.Address);

            // Prevent repeating the same update
            if (device.KeyMismatch) return;

            device.KeyMismatch = true;

            OnUpdatedEndpoints();
        });
    }

    private void AddDevices()
    {
        foreach (var bus in _configuration.Buses)
        {
            foreach (var device in bus.Devices)
            {
                try
                {
                    AddDeviceToPanel(device);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    private void StartConnections()
    {
        foreach (var bus in _configuration.Buses)
        {
            _portMapping.TryAdd(bus.PortName,
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
                _logger?.LogWarning(exception, "Unable to deserialize {Configuration}", configuration);
                _configuration = new Configuration {Buses = new List<Bus>()};
            }
        }

        if (_configuration == null) return;
        
        _configuration.AvailablePorts = SerialPort.GetPortNames();
    }

    /// <inheritdoc />
    public void Unload()
    {
        foreach (var access in _endpoints.Where(endpoint => endpoint is OSDPAccess).Cast<OSDPAccess>())
        {
            access.Dispose();
        }
        
        _panel.Shutdown();
            
        _panel.ConnectionStatusChanged -= PanelOnConnectionStatusChanged;
        _panel.RawCardDataReplyReceived -= PanelOnRawCardDataReplyReceived;
        _panel.InputStatusReportReplyReceived -= PanelOnInputStatusReportReplyReceived;
        _panel.OutputStatusReportReplyReceived -= PanelOnOutputStatusReportReplyReceived;
        _panel.NakReplyReceived -= PanelOnNakReplyReceived;
        
        _pkocPanel.CardPresented -= PkocPanelOnCardPresented;
    }

    /// <inheritdoc />
    public string CurrentConfiguration()
    {
        return JsonConvert.SerializeObject(_configuration);
    }

    /// <inheritdoc />
    public string ScrubSensitiveConfigurationData(string jsonConfigurationString)
    {
        var configuration  = JsonConvert.DeserializeObject<Configuration>(jsonConfigurationString);
        configuration.Buses.ForEach(bus => bus.Devices.ForEach(device => device.SecurityKey = null));
        return JsonConvert.SerializeObject(configuration);
    }

    public async Task<string> PerformAction(string action, string parameters)
    {
        Enum.TryParse(action, out ActionType actionEnum);

        if (actionEnum == ActionType.AvailablePorts)
        {
            _configuration.AvailablePorts = SerialPort.GetPortNames();
        }
        
        return actionEnum switch
        {
            ActionType.AddBus => AddBus(parameters),
            ActionType.RemoveBus => RemoveBus(parameters),
            ActionType.AddUpdateDevice => AddUpdateDevice(parameters),
            ActionType.RemoveDevice => RemoveDevice(parameters),
            ActionType.AvailablePorts => JsonConvert.SerializeObject(SerialPort.GetPortNames()),
            ActionType.ClearDeviceIdentity => ClearDeviceIdentity(parameters),
            ActionType.ResetToClear => ResetToClear(parameters),
            ActionType.ResetToInstallMode => ResetToInstallMode(parameters),
            ActionType.RotateKey => await RotateKey(parameters),
            ActionType.EnablePKOC => UpdatePKOCSetting(parameters, true),
            ActionType.DisablePKOC => UpdatePKOCSetting(parameters, false),
            _ => await Task.FromResult(string.Empty).ConfigureAwait(false)
        };
    }

    public event EventHandler<EventArgs> UpdatedEndpoints;
    public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;
    public event EventHandler<StateChangedEventArgs> StateChanged;
    public event EventHandler<OnlineStatusChangedEventArgs> OnlineStatusChanged;

    private string AddBus(string parameters)
    {
        var busAction = JsonConvert.DeserializeObject<BusAction>(parameters);
        if (busAction == null) return string.Empty;

        _configuration.Buses.Add(busAction.Bus);

        if (!_portMapping.ContainsKey(busAction.Bus.PortName))
        {
            _portMapping.TryAdd(busAction.Bus.PortName,
                _panel.StartConnection(new SerialPortOsdpConnection(busAction.Bus.PortName, busAction.Bus.BaudRate)));
        }

        return string.Empty;
    }
        
    private string RemoveBus(string parameters)
    {
        var busAction = JsonConvert.DeserializeObject<BusAction>(parameters);
        if (busAction == null) return string.Empty;

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
        if (deviceAction == null) return string.Empty;

        _configuration.Buses.First(bus => bus.PortName == deviceAction.Device.PortName).Devices
            .Add(deviceAction.Device);

        AddDeviceToPanel(deviceAction.Device);

        return string.Empty;
    }

    private string RemoveDevice(string parameters)
    {
        var deviceAction = JsonConvert.DeserializeObject<DeviceAction>(parameters);
        if (deviceAction == null) return string.Empty;

        _endpoints.RemoveAll(endpoint => endpoint.Id.Split(':').First() == deviceAction.Device.Address.ToString());
        OnUpdatedEndpoints();

        _configuration.Buses.First(bus => bus.PortName == deviceAction.Device.PortName).Devices
            .RemoveAll(device => device.Address == deviceAction.Device.Address);

        _panel.RemoveDevice(_portMapping[deviceAction.Device.PortName], deviceAction.Device.Address);

        return string.Empty;
    }
    
    private string ClearDeviceIdentity(string parameters)
    {
        var deviceAction = JsonConvert.DeserializeObject<DeviceAction>(parameters);
        if (deviceAction == null) return string.Empty;

        var foundDevice = _configuration.Buses.First(bus => bus.PortName == deviceAction.Device.PortName).Devices
            .First(device => device.Address == deviceAction.Device.Address);
        
        _logger?.LogInformation("Clearing identity for device '{DeviceName}'", foundDevice.Name);
        
        foundDevice.Identification = null;
        
        _panel.RemoveDevice(_portMapping[foundDevice.PortName], foundDevice.Address);
        AddDeviceToPanel(foundDevice);

        return string.Empty;
    }
    
    private string ResetToClear(string parameters)
    {
        var deviceAction = JsonConvert.DeserializeObject<DeviceAction>(parameters);
        if (deviceAction == null) return string.Empty;
        
        var foundDevice = _configuration.Buses.First(bus => bus.PortName == deviceAction.Device.PortName).Devices
            .First(device => device.Address == deviceAction.Device.Address);
        
        _logger?.LogInformation("Setting device '{DeviceName}' security to clear text", foundDevice.Name);

        foundDevice.RequireSecurity = false;
        foundDevice.LastKeyRotation = default;
        
        _panel.RemoveDevice(_portMapping[foundDevice.PortName], foundDevice.Address);
        AddDeviceToPanel(foundDevice);

        return string.Empty;
    }

    private string ResetToInstallMode(string parameters)
    {
        var deviceAction = JsonConvert.DeserializeObject<DeviceAction>(parameters);
        if (deviceAction == null) return string.Empty;
        
        var foundDevice = _configuration.Buses.First(bus => bus.PortName == deviceAction.Device.PortName).Devices
            .First(device => device.Address == deviceAction.Device.Address);
        
        _logger?.LogInformation("Setting device '{DeviceName}' security to install mode", foundDevice.Name);

        foundDevice.RequireSecurity = true;
        foundDevice.LastKeyRotation = default;

        _panel.RemoveDevice(_portMapping[foundDevice.PortName], foundDevice.Address);
        AddDeviceToPanel(foundDevice);

        return string.Empty;
    }

    private async Task<string> RotateKey(string parameters)
    {
        var deviceAction = JsonConvert.DeserializeObject<DeviceAction>(parameters);
        if (deviceAction == null) return string.Empty;

        var foundDevice = _configuration.Buses.First(bus => bus.PortName == deviceAction.Device.PortName).Devices
            .First(device => device.Address == deviceAction.Device.Address);
        
        _logger?.LogInformation("Rotating the key for device '{DeviceName}'", foundDevice.Name);
        
        var newKey = CreateRandomKey();

        try
        {
            bool keySetSuccess = await _panel.EncryptionKeySet(_portMapping[foundDevice.PortName],
                foundDevice.Address,
                new EncryptionKeyConfiguration(KeyType.SecureChannelBaseKey,
                    newKey)).ConfigureAwait(false);

            if (keySetSuccess)
            {
                foundDevice.RequireSecurity = true;
                foundDevice.SecurityKey = _dataEncryption.Encrypt(BitConverter.ToString(newKey).Replace("-", ""));
                foundDevice.LastKeyRotation = DateTime.UtcNow;

                _panel.RemoveDevice(_portMapping[foundDevice.PortName], foundDevice.Address);
                AddDeviceToPanel(foundDevice);
            }
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Unable to rotate the security key for \'{DeviceName}\'",
                foundDevice.Name);
        }

        return string.Empty;
    }
    
    private string UpdatePKOCSetting(string parameters, bool pkocEnabled)
    {
        var deviceAction = JsonConvert.DeserializeObject<DeviceAction>(parameters);
        if (deviceAction == null) return string.Empty;
        
        var foundDevice = _configuration.Buses.First(bus => bus.PortName == deviceAction.Device.PortName).Devices
            .First(device => device.Address == deviceAction.Device.Address);
        
        _logger?.LogInformation("Setting PKOC enable on '{DeviceName}' to {PkocEnabled}", foundDevice.Name, pkocEnabled);

        foundDevice.PKOCEnabled = pkocEnabled;
        
        _panel.RemoveDevice(_portMapping[foundDevice.PortName], foundDevice.Address);
        AddDeviceToPanel(foundDevice);
        
        return string.Empty;
    }

    private byte[] CreateRandomKey()
    {
        int keySizeInBytes = 16;
        byte[] randomKey = new byte[keySizeInBytes];

        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomKey);

        return randomKey;
    }

    private void AddDeviceToPanel(Device device)
    {
        _panel.AddDevice(_portMapping[device.PortName], device.Address, true, device.RequireSecurity, 
            CheckSecurityKey(device));
    }
    
    private byte[] CheckSecurityKey(Device device)
    {
        if (device.SecureMode != SecureMode.Secure) return null;

        try
        {
            return Convert.FromHexString(_dataEncryption.Decrypt(device.SecurityKey));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to get decrypt the security key for \'{DeviceName}\'",
                device.Name);

            throw;
        }
    }

    protected virtual void OnUpdatedEndpoints()
    {
        UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnOnlineStatusChanged(OnlineStatusChangedEventArgs e)
    {
        OnlineStatusChanged?.Invoke(this, e);
    }
}