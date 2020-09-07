using System;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aporta.Workers
{
    public class StartupWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<StartupWorker> _logger;
        private readonly IDataAccess _dataAccess;
        private readonly ExtensionService _extensionService;
        private readonly AccessService _accessService;

        public StartupWorker(IDataAccess dataAccess, ExtensionService extensionService, AccessService accessService, ILogger<StartupWorker> logger, IHostApplicationLifetime applicationLifetime)
        {
            _dataAccess = dataAccess;
            _extensionService = extensionService;
            _accessService = accessService;
            _logger = logger;
            _applicationLifetime = applicationLifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Application is starting up.");
            try
            {
                await _dataAccess.UpdateSchema();
                await _extensionService.Startup();
                _accessService.Startup();
            }
            catch (Exception exception)
            {
                _logger.LogCritical(exception, "An error occurred during startup.");
                _applicationLifetime.StopApplication();
            }
            _logger.LogInformation("Application completed startup routine.");

            stoppingToken.Register(() =>
            {
                _logger.LogWarning("Application is shutting down.");
                _accessService.Shutdown();
                _extensionService.Shutdown();
            });
        }
    }
}
