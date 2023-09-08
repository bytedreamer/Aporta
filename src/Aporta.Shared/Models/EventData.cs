namespace Aporta.Shared.Models;

public class EventData
{
    public EventReason EventReason { get; set; }
    public Person Person { get; set; }
    public Door Door { get; set; }
    public Endpoint Endpoint { get; set; }
}

public enum EventReason
{
    [Description("Credential Not Enrolled")]
    CredentialNotEnrolled
}