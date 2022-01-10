using Aporta.Extensions.Endpoint;
// ReSharper disable UnassignedGetOnlyAutoProperty

namespace Aporta.Extensions.Hardware
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class OnlineStatusChangedEventArgs
    {
        public IEndpoint Endpoint { get; }
        
        public bool IsOnline { get; }
    }
}