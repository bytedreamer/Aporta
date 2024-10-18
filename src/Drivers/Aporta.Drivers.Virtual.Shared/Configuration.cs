namespace Aporta.Drivers.Virtual.Shared;

public class Configuration
{
    public List<Device> Readers { get; } = [];
    
    public List<Device> Outputs { get; } = [];
    
    public List<Device> Inputs { get; } = [];
}