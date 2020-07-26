using System;
using System.Threading.Tasks;

namespace Aporta.Core.Services
{
    public class OutputService
    {
        private readonly ExtensionService _extensionService;

        public OutputService(ExtensionService extensionService)
        {
            _extensionService = extensionService;
        }
        
        public async Task Set(Guid extensionId, string driverId, bool state)
        {
            await _extensionService.GetControlPoint(extensionId, driverId).Set(state);
        }

        public async Task<bool?> Get(Guid extensionId, string driverId)
        {
            return await _extensionService.GetControlPoint(extensionId, driverId).Get();
        }
    }
}