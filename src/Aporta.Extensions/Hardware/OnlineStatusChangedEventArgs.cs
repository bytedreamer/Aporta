using Aporta.Extensions.Endpoint;
// ReSharper disable UnassignedGetOnlyAutoProperty

namespace Aporta.Extensions.Hardware;

public class OnlineStatusChangedEventArgs
{
    public IEndpoint Endpoint { get; }

    public bool IsOnline { get; }

    public OnlineStatusChangedEventArgs(IEndpoint endpoint, bool isOnline)
    {
        Endpoint = endpoint;
        IsOnline = isOnline;
    }
}