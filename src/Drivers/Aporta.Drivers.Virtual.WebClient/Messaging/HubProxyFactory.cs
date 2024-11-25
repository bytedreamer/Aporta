using System;

namespace Aporta.Drivers.Virtual.Messaging;

public class HubProxyFactory : IHubProxyFactory
{
    public IHubProxy Create(Uri uri)
    {
        return new HubProxy(uri);
    }
}

public interface IHubProxyFactory
{
    IHubProxy Create(Uri uri);
}