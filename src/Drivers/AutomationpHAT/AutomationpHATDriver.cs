using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Threading.Tasks;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;

namespace Aporta.Drivers.AutomationpHAT
{
    /// <summary>
    /// 
    /// </summary>
    public class AutomationpHATDriver : IHardwareDriver
    {
        private GpioController _controller;
        private readonly List<IEndpoint> _endpoints = new List<IEndpoint>();

        public Guid Id => Guid.Parse("3803BA97-C8E3-479D-993B-E76DAB9ABED6");

        public IEnumerable<IEndpoint> Endpoints => _endpoints;

        public string Name => "Automation pHAT";

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            _controller = new GpioController();

            _endpoints.Add(new Relay(Id,"Relay1", _controller, 16));
        }

        public void Unload()
        {
            //Endpoints.Clear();
                
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
    }
}