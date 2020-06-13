using System;
using System.Collections.Generic;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using OSDP.Net;
using OSDP.Net.Connections;

namespace Aporta.Drivers.OSDP
{
    /// <summary>
    /// 
    /// </summary>
    public class OSDPDriver : IHardwareDriver
    {
        private readonly ControlPanel _panel = new ControlPanel();
        private readonly List<IEndpoint> _endpoints = new List<IEndpoint>();

        public Guid Id => Guid.Parse("D3C5DE68-E019-48D6-AB58-76F4B15CD0D5");

        public string Name => "OSDP";

        public IEnumerable<IDevice> Devices => new List<IDevice>();

        public IEnumerable<IEndpoint> Endpoints => _endpoints;

        public void Load()
        {
            Guid connection =
                _panel.StartConnection(new SerialPortOsdpConnection("/dev/tty.usbserial-AB0JI236", 9600));

            _panel.AddDevice(connection, 1, true, false);

            _endpoints.Add(new OSDPControlPoint(_panel, connection, 1, 0));
        }

        public void Unload()
        {
            _panel.Shutdown();
        }
    }
}