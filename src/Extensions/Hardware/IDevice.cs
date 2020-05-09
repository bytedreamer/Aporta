using System.Collections.Generic;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware
{
    public interface IDevice : IExtension
    {
        IEnumerable<IEndpoint> Endpoints { get; }
    }
}