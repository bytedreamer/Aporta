using System;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Drivers.OSDP.Shared;
using Aporta.Extensions.Endpoint;
using OSDP.Net;

namespace Aporta.Drivers.OSDP;

public class OSDPInput : IInput
{
    private readonly ControlPanel _controlPanel;
    private readonly Guid _connectionId;
    private readonly Device _device;
    private readonly Input _input;

    public OSDPInput(Guid extensionId, ControlPanel controlPanel, Guid connectionId, Device device, Input input)
    {
        _controlPanel = controlPanel;
        _connectionId = connectionId;
        _device = device;
        _input = input;
        ExtensionId = extensionId;
    }

    public string Name => _input.Name;

    public Guid ExtensionId { get; }
        
    public string Id => $"{_device.PortName}:{_device.Address}:I{_input.Number}";

    public async Task<bool> GetState()
    {
        return (await _controlPanel.InputStatus(_connectionId, _device.Address).ConfigureAwait(false)).InputStatuses.ToArray()[
            _input.Number];
    }

    public async Task SetState(bool value)
    {
        return; //Do nothing
    }

    public Task<bool> GetOnlineStatus()
    {
        return Task.FromResult(true);
    }
}