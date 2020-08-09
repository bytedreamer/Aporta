using System.Collections.Concurrent;

namespace Aporta.Drivers.OSDP.Shared
{
    public class Device
    {
        public string Name { get; set; }
        
        public byte Address { get; set; }
        
        public bool RequireSecurity { get; set; }

        public bool CheckedCapabilities { get; set; }

        public ConcurrentBag<Input> Inputs { get; } = new ConcurrentBag<Input>();
        public ConcurrentBag<Output> Outputs { get; } = new ConcurrentBag<Output>();
        public ConcurrentBag<Reader> Readers { get; } = new ConcurrentBag<Reader>();
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