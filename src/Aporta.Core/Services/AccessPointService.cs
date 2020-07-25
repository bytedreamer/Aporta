using System;
using Aporta.Extensions.Hardware;

namespace Aporta.Core.Services
{
    public class AccessPointService
    {
        public AccessPointService(ExtensionService extensionService)
        {
            extensionService.AccessCredentialReceived += ExtensionServiceOnAccessCredentialReceived;
        }
        
        public event EventHandler<AccessCredentialReceivedEventArgs> AccessCredentialReceived;

        private void ExtensionServiceOnAccessCredentialReceived(object sender, AccessCredentialReceivedEventArgs eventArgs)
        {
            AccessCredentialReceived?.Invoke(this, eventArgs);
        }
    }
}