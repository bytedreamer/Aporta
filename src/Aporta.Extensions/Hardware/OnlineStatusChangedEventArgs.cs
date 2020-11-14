using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware
{
    public class OnlineStatusChangedEventArgs
    {
        public IEndpoint Endpoint { get; }
        
        public bool IsOnline { get; }
    }
}