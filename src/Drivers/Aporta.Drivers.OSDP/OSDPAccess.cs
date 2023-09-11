using System;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Drivers.OSDP.Shared;
using OSDP.Net;
using OSDP.Net.Model.CommandData;

namespace Aporta.Drivers.OSDP;

public class OSDPAccess : IAccess
{
    private readonly Device _device;
    private readonly Reader _reader;
    private readonly ControlPanel _panel;
    private readonly Guid _connectionId;

    public OSDPAccess(Guid extensionId, Device device, Reader reader, ControlPanel panel, Guid connectionId)
    {
        _device = device;
        _reader = reader;
        _panel = panel;
        _connectionId = connectionId;
        ExtensionId = extensionId;
    }

    public string Name => _reader.Name;

    public Guid ExtensionId { get; }
        
    public string Id => $"{_device.PortName}:{_device.Address}:R{_reader.Number}";
        
    public Task<bool> GetOnlineStatus()
    {
        return Task.FromResult(_panel.IsOnline(_connectionId, _device.Address));
    }

    public async Task Beep()
    {
        await _panel.ReaderBuzzerControl(_connectionId, _device.Address,
            new ReaderBuzzerControl(_reader.Number, ToneCode.Default, 1, 1, 3));
    }
}