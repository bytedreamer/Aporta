namespace Aporta.Drivers.Virtual.Shared;

public class Configuration
{
    public List<Reader> Readers { get; } = [];
    
    public List<Output> Outputs { get; } = [];
    
    public List<Input> Inputs { get; } = [];
}