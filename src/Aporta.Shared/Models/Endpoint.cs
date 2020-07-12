using System;

namespace Aporta.Shared.Models
{
    public class Endpoint
    {
        public int Id { get; set; }
        
        public string Name { get; set; }
        
        
        public string DriverId { get; set; } = "";
        
        public Guid ExtensionId { get; set; }

        public EndpointType Type { get; set; }
    }

    public enum EndpointType
    {
        Output,
        Input,
        Reader
    }
}