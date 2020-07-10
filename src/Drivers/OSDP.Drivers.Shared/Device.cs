using System;
using System.Collections.Generic;

namespace OSDP.Drivers.Shared
{
    public class Device
    {
        public string Name { get; set; }
        
        public byte Address { get; set; }

        public bool CheckedCapabilities { get; set; }


        public List<Output> Outputs { get; set; } = new List<Output>();
    }

    public class Output
    {
        public string Name { get; set; }
        
        public byte Number { get; set; }
        
        public Guid EndPointId { get; set; }
    }
}