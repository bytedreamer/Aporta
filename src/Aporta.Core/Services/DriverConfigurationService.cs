using System;

namespace Aporta.Core.Services
{
    public class DriverConfigurationService : IDriverConfigurationService
    {
        private readonly IMainService _mainService;

        public DriverConfigurationService(IMainService mainService)
        {
            _mainService = mainService;
        }

        public void UpdateSettings(Guid extensionId, string settings)
        {
            var driver = _mainService.Driver(extensionId);
        }
    }

    public interface IDriverConfigurationService
    {
        void UpdateSettings(Guid extensionId, string settings);
    }
}