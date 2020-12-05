using System;

namespace Aporta.Shared.Models
{
    public class Credential
    {
        public int Id { get; set; }
        
        public string Number { get; set; }
        
        public DateTime EnrollDate { get; set; } = DateTime.UtcNow;
    }
}