#nullable enable
namespace Aporta.Shared.Models;

public class EventData
{
    public EventReason EventReason { get; set; }
    
    public Person Person { get; set; }
    
    public Door Door { get; set; }
    
    public Endpoint Endpoint { get; set; }
    
    public string? CardNumber { get; set; }
}

public enum EventReason
{
    [Description("None")]
    None,
    [Description("Door Used")]
    DoorUsed,
    [Description("Door Not Used")]
    DoorNotUsed,
    [Description("Access Not Assigned")]
    AccessNotAssigned,
    [Description("Credential Not Enrolled")]
    CredentialNotEnrolled
}