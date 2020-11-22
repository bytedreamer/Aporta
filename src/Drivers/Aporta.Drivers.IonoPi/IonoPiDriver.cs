using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;

namespace Aporta.Drivers.IonoPi
{
    /// <summary>
    /// 
    /// </summary>
    public class IonoPiDriver : IHardwareDriver
    {
        private GpioController _controller;
        private readonly List<IEndpoint> _endpoints = new List<IEndpoint>();
        private readonly int[] _relayPins = {17, 27, 22, 23};

        public Guid Id => Guid.Parse("3803BA97-C8E3-479D-993B-E76DAB9ABED6");

        public IEnumerable<IEndpoint> Endpoints => _endpoints;

        public string Name => "Iono Pi RTC board";

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<IonoPiDriver>();
            
            _controller = new GpioController();

            for (var index = 0; index < _relayPins.Length; index++)
            {
                _endpoints.Add(new Relay(Id, $"Relay{index + 1}", _controller, _relayPins[index]));
                _controller.RegisterCallbackForPinValueChangedEvent(_relayPins[index],
                    PinEventTypes.Falling | PinEventTypes.Rising, PinStateChangedCallback);
            }

            OnUpdatedEndpoints();
        }

        private void PinStateChangedCallback(object sender, PinValueChangedEventArgs eventArgs)
        {
            OnStateChanged(_endpoints.Single(endpoint => endpoint.Id == eventArgs.PinNumber.ToString()),
                eventArgs.ChangeType == PinEventTypes.Rising);
        }

        public void Unload()
        {
            foreach (int relayPin in _relayPins)
            {
                _controller.UnregisterCallbackForPinValueChangedEvent(relayPin, PinStateChangedCallback);
            }
            
            _endpoints.Clear();
                
            _controller?.Dispose();
        }

        public string CurrentConfiguration()
        {
            return string.Empty;
        }

        public async Task<string> PerformAction(string action, string parameters)
        {
            return await Task.FromResult(string.Empty);
        }

        public event EventHandler<EventArgs> UpdatedEndpoints;
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;
        public event EventHandler<StateChangedEventArgs> StateChanged;
        public event EventHandler<OnlineStatusChangedEventArgs> OnlineStatusChanged;

        protected virtual void OnUpdatedEndpoints()
        {
            UpdatedEndpoints?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnOnlineStatusChanged(OnlineStatusChangedEventArgs e)
        {
            OnlineStatusChanged?.Invoke(this, e);
        }

        protected virtual void OnStateChanged(IEndpoint endpoint, bool state)
        {
            StateChanged?.Invoke(this, new StateChangedEventArgs(endpoint, state));
        }

        protected virtual void OnAccessCredentialReceived(AccessCredentialReceivedEventArgs e)
        {
            AccessCredentialReceived?.Invoke(this, e);
        }
    }
}