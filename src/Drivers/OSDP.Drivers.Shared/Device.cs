using System.Collections.Generic;

namespace OSDP.Drivers.Shared
{
    public class Device
    {
        public string Name { get; set; }
        
        public byte Address { get; set; }

        public bool CheckedCapabilities { get; set; }

        public List<Output> Outputs { get; set; } = new List<Output>();
        public List<Reader> Readers { get; set; } = new List<Reader>();
    }

    public class Output
    {
        public string Name { get; set; }
        
        public byte Number { get; set; }
    }
    
    public class Reader
    {
        public string Name { get; set; }
        
        public byte Number { get; set; }
    }
}