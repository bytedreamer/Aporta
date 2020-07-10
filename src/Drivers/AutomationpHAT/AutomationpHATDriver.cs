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
        
        public string Name => "Automation pHAT";
        
        public IEnumerable<IEndpoint> Endpoints => _endpoints;
        
        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            _controller = new GpioController();

            _endpoints.Add(new Relay(1, "Relay1", _controller, 16));
        }

        public void Unload()
        {
            _endpoints.Clear();
                
            _controller?.Dispose();
        }

        public string InitialConfiguration()
        {
            return string.Empty;
        }

        public async Task<string> PerformAction(string action, string parameters)
        {
            return await Task.FromResult(string.Empty);
        }
    }
}