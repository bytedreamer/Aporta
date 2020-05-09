using System.Collections.Generic;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware
{
    public interface IHardwareDriver : IExtension
    {
        public IEnumerable<IBus> Buses { get; }
        
        public IEnumerable<IEndpoint> Endpoints { get; }
        
        void Load();
        
        void Unload();
    }
}