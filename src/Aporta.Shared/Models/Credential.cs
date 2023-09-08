namespace Aporta.Shared.Models;

public class Credential
{
    public int Id { get; set; }
        
    public string Number { get; set; }
        
    public int LastEvent { get; set; }
        
    public int? AssignedPersonId { get; set; }
        
    public bool? Enabled { get; set; }
}