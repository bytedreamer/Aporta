using System.Collections.Generic;

namespace Aporta.Drivers.OSDP.Shared
{
    public class Device
    {
        public string Name { get; set; }
        
        public byte Address { get; set; }
        
        public bool RequireSecurity { get; set; }

        public bool CheckedCapabilities { get; set; }
        
        public bool IsConnected { get; set; }

        public List<Input> Inputs { get; } = new List<Input>();
        public List<Output> Outputs { get; } = new List<Output>();
        public List<Reader> Readers { get; } = new List<Reader>();
    }
    
    public class Input
    {
        public string Name { get; set; }
        
        public byte Number { get; set; }
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