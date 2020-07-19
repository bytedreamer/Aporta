using System;

namespace Aporta.Extensions.Endpoint
{
    public interface IAccessPoint : IEndpoint
    {
        event EventHandler<byte[]> AccessCredentialReceived;
    }
}