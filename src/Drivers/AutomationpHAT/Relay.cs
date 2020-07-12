using System;
using System.Device.Gpio;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;

namespace Aporta.Drivers.AutomationpHAT
{
    public class Relay : IControlPoint
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
        
        public async Task Set(bool state)
        {
            _controller.Write(_pin, state ? PinValue.High : PinValue.Low);
            await Task.CompletedTask;
        }
    }
}