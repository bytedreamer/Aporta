using System;

namespace Aporta.Shared.Models;

public class Event
{
    public int Id { get; set; }

    public int EndpointId { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public EventType Type { get; set; }

    public string Data { get; set; } = "";
}

public enum EventType
{
    AccessGranted,
    AccessDenied
}