using System;
using System.Collections.Generic;
using Aporta.Extensions.Endpoint;
using Microsoft.Extensions.Logging;

namespace Aporta.Extensions.Hardware
{
    public interface IHardwareDriver : IExtension
    {
        public Guid Id { get; }
        
        public IEnumerable<IDevice> Devices { get; }
        
        public IEnumerable<IEndpoint> Endpoints { get; }
        
        void Load(string settings, ILoggerFactory loggerFactory);
        
        void Unload();
    }
}