using System.Collections.Generic;

namespace Aporta.Drivers.OSDP.Shared
{
    public class Configuration
    {
        public string[] AvailablePorts { get; set; }
        
        public List<Bus> Buses { get; set; } = new();
    }
}