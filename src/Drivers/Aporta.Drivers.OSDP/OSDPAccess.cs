using System;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Drivers.OSDP.Shared;
using OSDP.Net;
using OSDP.Net.Model.CommandData;

namespace Aporta.Drivers.OSDP;

public class OSDPAccess : IAccess, IDisposable, IAsyncDisposable
{
    private readonly Device _device;
    private readonly Reader _reader;
    private readonly ControlPanel _panel;
    private readonly Guid _connectionId;
    private readonly Timer _heartbeatTimer;

    public OSDPAccess(Guid extensionId, Device device, Reader reader, ControlPanel panel, Guid connectionId)
    {
        _device = device;
        _reader = reader;
        _panel = panel;
        _connectionId = connectionId;
        ExtensionId = extensionId;
        
        _heartbeatTimer = new(FlashHeartbeat , null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public string Name => _reader.Name;

    public Guid ExtensionId { get; }
        
    public string Id => $"{_device.PortName}:{_device.Address}:R{_reader.Number}";
        
    public Task<bool> GetOnlineStatus()
    {
        return Task.FromResult(_panel.IsOnline(_connectionId, _device.Address));
    }

    public async Task AccessGrantedNotification()
    {
        var accessGrantedNotificationTasks = new[]
        {
            _panel.ReaderLedControl(_connectionId, _device.Address,
                new ReaderLedControls(new[]
                {
                    new ReaderLedControl(_reader.Number, 0, TemporaryReaderControlCode.SetTemporaryAndStartTimer, 1, 1,
                        LedColor.Green, LedColor.Green, 30, PermanentReaderControlCode.SetPermanentState, 1, 1,
                        LedColor.Red,
                        LedColor.Red)
                })),
            _panel.ReaderBuzzerControl(_connectionId, _device.Address,
                new ReaderBuzzerControl(_reader.Number, ToneCode.Default, 1, 1, 3))
        };
        
        await Task.WhenAll(accessGrantedNotificationTasks);
    }

    private async void FlashHeartbeat(object o)
    {
        try
        {
            if (_panel.IsOnline(_connectionId, _device.Address))
            {
                await _panel.ReaderLedControl(_connectionId, _device.Address,
                    new ReaderLedControls(new[]
                    {
                        new ReaderLedControl(_reader.Number, 0, TemporaryReaderControlCode.SetTemporaryAndStartTimer, 1, 1,
                            LedColor.Black, LedColor.Black, 5, PermanentReaderControlCode.SetPermanentState, 1, 1, LedColor.Red,
                            LedColor.Red)
                    }));
            }
        }
        catch
        {
            // ignored
        }
    }

    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_heartbeatTimer != null) await _heartbeatTimer.DisposeAsync();
    }
}