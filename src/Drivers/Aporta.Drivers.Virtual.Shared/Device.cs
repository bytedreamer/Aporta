namespace Aporta.Drivers.Virtual.Shared;

public class Device
{
    public string Name { get; init; } = string.Empty;
        
    public byte Number { get; init; }

	public bool State { get; set; }
}
