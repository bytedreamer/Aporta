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

        public Relay(Guid id, string name, GpioController controller, int pin)
        {
            _controller = controller;
            _pin = pin;
            Id = id;
            Name = name;
            
            _controller.OpenPin(pin, PinMode.Output);
        }

        public string Name { get; }
        
        public Guid Id { get; }
        
        public async Task Set(bool state)
        {
            _controller.Write(_pin, state ? PinValue.High : PinValue.Low);
            await Task.CompletedTask;
        }
    }
}