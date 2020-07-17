using System;
using System.Device.Gpio;
using System.Threading.Tasks;
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

        public Guid Id => Guid.Parse("3803BA97-C8E3-479D-993B-E76DAB9ABED6");
        
        public string Name => "Automation pHAT";

        public void Load(string configuration, ILoggerFactory loggerFactory)
        {
            _controller = new GpioController();

            //Endpoints.Add(new Relay("Relay1", _controller, 16));
        }

        public void Unload()
        {
            //Endpoints.Clear();
                
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

        public event EventHandler<AddEndpointsEventArgs> AddEndpoints;
    }
}