using System.Collections.Generic;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;

namespace Aporta.OSDP
{
    public class OSDPDevice : IDevice
    {
        public string Name { get; }
        
        public IEnumerable<IEndpoint> Endpoints { get; }
    }
}