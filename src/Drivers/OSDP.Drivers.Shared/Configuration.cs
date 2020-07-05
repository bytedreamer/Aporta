using System.Collections.Generic;

namespace OSDP.Drivers.Shared.Actions
{
    public class Configuration
    {
        public string[] AvailablePorts { get; set; }
        
        public List<Bus> Buses { get; set; } = new List<Bus>();
    }
}