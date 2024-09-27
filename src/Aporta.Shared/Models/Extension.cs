using System;

namespace Aporta.Shared.Models;

public class Extension
{
    public Guid Id { get; set; }
        
    public string Name { get; set; }
        
    public bool Enabled { get; set; }

    public string Configuration { get; set; } = "";
        
    /// <summary>
    /// Has the extension been loaded 
    /// </summary>
    public bool Loaded { get; set; }
    
    public Extension ShallowCopy()
    {
        return (Extension) MemberwiseClone();
    }
}