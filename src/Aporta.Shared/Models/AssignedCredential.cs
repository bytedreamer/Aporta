namespace Aporta.Shared.Models
{
    public class AssignedCredential : Credential
    {
        public bool Enabled { get; set; }
        
        public Person Person { get; set; }
    }
}