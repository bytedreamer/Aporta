using System;
using System.Collections.Generic;
using Aporta.Extensions.Endpoint;

namespace Aporta.Extensions.Hardware
{
    public interface IHardwareDriver : IExtension
    {
        public Guid Id { get; }
        
        public IEnumerable<IDevice> Devices { get; }
        
        public IEnumerable<IEndpoint> Endpoints { get; }
        
        void Load();
        
        void Unload();
    }
}