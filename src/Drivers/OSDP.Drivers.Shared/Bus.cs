using System.Collections.Generic;

namespace OSDP.Drivers.Shared.Actions
{
    public class Bus
    {
        public string PortName { get; set; }
        
        public int BaudRate { get; set; }
        
        public List<Device> Devices { get; set; } = new List<Device>();
    }
}