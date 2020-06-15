using System;
using System.Collections.Generic;
using System.Device.Gpio;
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
        
        public IEnumerable<IDevice> Devices => new List<IDevice>();
        
        public IEnumerable<IEndpoint> Endpoints => _endpoints;
        
        public void Load(ILoggerFactory loggerFactory)
        {
            _controller = new GpioController();

            _endpoints.Add(new Relay(Guid.NewGuid(), "Relay1", _controller, 16));
        }

        public void Unload()
        {
            _endpoints.Clear();
                
            _controller?.Dispose();
        }
    }
}