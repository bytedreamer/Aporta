using System;
using System.Collections.Generic;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using OSDP.Net;
using OSDP.Net.Connections;

namespace Aporta.OSDP
{
    /// <summary>
    /// 
    /// </summary>
    public class Driver : IHardwareDriver
    {
        private readonly ControlPanel _panel;

        public Driver()
        {
            _panel = new ControlPanel();
        }

        public string Name => "OSDP";
        
        public IEnumerable<IBus> Buses { get; }
        
        public IEnumerable<IEndpoint> Endpoints { get; }

        public void Load()
        {
            Guid connection =
                _panel.StartConnection(new SerialPortOsdpConnection("/dev/tty.usbserial-AB0JI236", 9600));
            _panel.AddDevice(connection, 1, true, false);
        }

        public void Unload()
        {
            _panel.Shutdown();
        }
    }
}