using System;
using Aporta.Extensions.Hardware;

namespace Aporta.Core.Services
{
    public class AccessPointService : IDisposable
    {
        private readonly ExtensionService _extensionService;
        
        public AccessPointService(ExtensionService extensionService)
        {
            _extensionService = extensionService;
            _extensionService.AccessCredentialReceived += ExtensionServiceOnAccessCredentialReceived;
        }
        
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;

        private void ExtensionServiceOnAccessCredentialReceived(object sender, AccessCredentialReceivedEventArgs eventArgs)
        {
            AccessCredentialReceived?.Invoke(this, eventArgs);
        }

        public void Dispose()
        {
            _extensionService.AccessCredentialReceived -= ExtensionServiceOnAccessCredentialReceived;
        }
    }
}