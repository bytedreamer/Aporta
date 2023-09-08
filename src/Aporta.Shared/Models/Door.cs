namespace Aporta.Shared.Models;

public class Door
{
    public int Id { get; set; }
        
    public int? InAccessEndpointId { get; set; }
    public int? OutAccessEndpointId { get; set; }
    public int? DoorContactEndpointId { get; set; }
    public int? RequestToExitEndpointId { get; set; }
    public int? DoorStrikeEndpointId { get; set; }
        
    public string Name { get; set; }
}