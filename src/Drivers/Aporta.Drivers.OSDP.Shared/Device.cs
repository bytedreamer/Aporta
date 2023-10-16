using System.Collections.Generic;

namespace Aporta.Drivers.OSDP.Shared;

public class Device
{
    public string Name { get; set; } 
        
    public byte Address { get; set; }
        
    public string PortName { get; set; }
        
    public bool RequireSecurity { get; set; }

    public bool IdentityNotMatched { get; set; }
    
    public bool CheckedCapabilities { get; set; }
        
    public bool IsConnected { get; set; }
    
    public Identification Identification { get; set; }

    public List<Input> Inputs { get; } = new();
    public List<Output> Outputs { get; } = new();
    public List<Reader> Readers { get; } = new();
}

public class Identification
{
    public string VendorCode { get; init; }
    
    public int SerialNumber { get; init; }
    
    public string FirmwareVersion { get; init; }
}

public class Input
{
    public string Name { get; init; }
        
    public byte Number { get; init; }
}

public class Output
{
    public string Name { get; init; }
        
    public byte Number { get; init; }
}
    
public class Reader
{
    public string Name { get; init; }
        
    public byte Number { get; init; }
}