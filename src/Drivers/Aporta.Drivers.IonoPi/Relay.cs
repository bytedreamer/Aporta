using System;
using System.Device.Gpio;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.IonoPi;

public class Relay : IOutput
{
    private readonly GpioController _controller;
    private readonly int _pin;

    public Relay(Guid extensionId, string name, GpioController controller, int pin)
    {
        _controller = controller;
        _pin = pin;
        Name = name;
        ExtensionId = extensionId;
            
        _controller.OpenPin(pin, PinMode.Output);
    }

    public string Name { get; }
        
    public Guid ExtensionId { get; }
        
    public string Id => _pin.ToString();
        
    public Task<bool> GetOnlineStatus()
    {
        return Task.FromResult(true);
    }

    public Task<bool> GetState()
    {
        return Task.FromResult(_controller.Read(_pin) == PinValue.High);
    }

    public Task SetState(bool state)
    {
        _controller.Write(_pin, state ? PinValue.High : PinValue.Low);
        return Task.CompletedTask;
    }
}