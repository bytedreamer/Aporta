using System;
using System.Threading.Tasks;

namespace Aporta.Core.Services
{
    public class ControlPointService
    {
        private readonly ExtensionService _extensionService;

        public ControlPointService(ExtensionService extensionService)
        {
            _extensionService = extensionService;
        }
        
        public async Task SetOutput(Guid extensionId, string driverId, bool state)
        {
            await _extensionService.GetControlPoint(extensionId, driverId).Set(state);
        }

        public async Task<bool?> GetOutput(Guid extensionId, string driverId)
        {
            return await _extensionService.GetControlPoint(extensionId, driverId).Get();
        }
    }
}