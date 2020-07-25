using System;
using System.Collections.Generic;
using Aporta.Extensions.Hardware;

namespace Aporta.Extensions.Endpoint
{
    public interface IAccessPoint : IEndpoint
    {
        event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;
    }
}