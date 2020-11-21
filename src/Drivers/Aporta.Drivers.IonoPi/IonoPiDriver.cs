using System;
using System.Collections.Generic;
using System.Device.Gpio;
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

        public Guid Id => Guid.Parse("3803BA97-C8E3-479D-993B-E76DAB9ABED6");

        public IEnumerable<IEndpoint> Endpoints => _endpoints;

        public string Name => "Iono Pi RTC board";

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<IonoPiDriver>();
            
            _controller = new GpioController();
            
            _endpoints.Add(new Relay(Id,"Relay1", _controller, 17));
            _endpoints.Add(new Relay(Id,"Relay2", _controller, 27));
            _endpoints.Add(new Relay(Id,"Relay3", _controller, 22));
            _endpoints.Add(new Relay(Id,"Relay4", _controller, 23));
            
            OnUpdatedEndpoints();
        }

        public void Unload()
        {
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

        protected virtual void OnStateChanged(StateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        protected virtual void OnAccessCredentialReceived(AccessCredentialReceivedEventArgs e)
        {
            AccessCredentialReceived?.Invoke(this, e);
        }
    }
}