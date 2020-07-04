using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Models;
using Aporta.Extensions.Hardware;

namespace Aporta.Core.Services
{
    public interface IMainService
    {
        IEnumerable<ExtensionHost> Extensions { get; }

        Task Startup();

        void Shutdown();
        
        Task EnableExtension(Guid extensionId, bool enabled);
        
        IHardwareDriver Driver(Guid extensionId);
        
        Task UpdateConfiguration(Guid extensionId, string configuration);
    }
}