using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aporta.Core.Models;

namespace Aporta.Core.Services
{
    public interface IMainService
    {
        IEnumerable<ExtensionHost> Extensions { get; }

        Task Startup();

        void Shutdown();
        
        Task EnableExtension(Guid extensionId, bool enabled);
    }
}