namespace Aporta.Drivers.Virtual.Shared;

public class Input
{
    public string Name { get; init; } = string.Empty;
        
    public byte Number { get; init; }
}

public class Output
{
    public string Name { get; init; } = string.Empty;
        
    public byte Number { get; init; }
}
    
public class Reader
{
    public Reader()
    {
    }

    public Reader(string name, byte number)
    {
        Name = name;
        Number = number;
    }

    public string Name { get; init; } = string.Empty;

    public byte Number { get; init; }
}

public class AddInputParameter
{
    public string Name { get; set; } = string.Empty;
}

public class AddOutputParameter
{
    public string Name { get; set; } = string.Empty;
}