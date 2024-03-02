#nullable enable
namespace Aporta.Shared.Models;

public class EventData
{
    public EventReason EventReason { get; init; }
    
    public Person? Person { get; init; }
    
    public Door? Door { get; init; }
    
    public required Endpoint Endpoint { get; init; }
    
    public string? CardNumber { get; init; }
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