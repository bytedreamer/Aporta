namespace Aporta.Drivers.Virtual.Shared;

public class Configuration
{
    public List<Reader> Readers { get; set; } = new();
    
    public List<Output> Outputs { get; set; } = new();
    
    public List<Input> Inputs { get; set; } = new();
}