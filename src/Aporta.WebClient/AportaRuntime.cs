namespace Aporta.WebClient;

public class AportaRuntime
{
    public AportaRuntime(bool isTesting = false)
    {
        IsTesting = isTesting;
    }
    
    public bool IsTesting { get; }
}