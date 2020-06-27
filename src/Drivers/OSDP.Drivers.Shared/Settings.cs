using System.Collections.Generic;

namespace OSDP.Drivers.Shared
{
    public class Settings
    {
        public string[] AvailablePorts { get; set; }
        
        public List<Bus> Buses { get; set; }
    }
}