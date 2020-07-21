using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aporta.Core.DataAccess;
using Aporta.Core.DataAccess.Repositories;
using Aporta.Core.Extension;
using Aporta.Core.Hubs;
using Aporta.Core.Models;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Aporta.Core.Services
{
    /// <summary>
    /// 
    /// </summary>
    public class ExtensionService
    {
        private static readonly SemaphoreSlim EndpointUpdateSemaphore = new SemaphoreSlim(1, 1);

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ExtensionService> _logger;
        private readonly ExtensionRepository _extensionRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly IHubContext<DataChangeNotificationHub> _hubContext;

        private readonly List<ExtensionHost> _extensions = new List<ExtensionHost>();

        public ExtensionService(IDataAccess dataAccess, IHubContext<DataChangeNotificationHub> hubContext,
            ILogger<ExtensionService> logger, ILoggerFactory loggerFactory)
        {
            _hubContext = hubContext;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _endpointRepository = new EndpointRepository(dataAccess);
            _extensionRepository = new ExtensionRepository(dataAccess);
        }

        public IEnumerable<ExtensionHost> Extensions => _extensions;

        public async Task Startup()
        {
            _logger.LogInformation("Starting extension service");

            await DiscoverExtensions();

            LoadExtensions();
        }

        public void Shutdown()
        {
            _logger.LogInformation("Shutting down extension service");

            UnloadExtensions();
        }

        public async Task EnableExtension(Guid extensionId, bool enabled)
        {
            try
            {
                var matchingExtension = _extensions.First(extension => extension.Id == extensionId);

                _logger.LogInformation($"{(enabled ? "Enabling" : "Disabling")} extension {matchingExtension.Name}");

                matchingExtension.Enabled = enabled;
                await _extensionRepository.Update(matchingExtension);

                if (enabled)
                {
                    LoadExtension(matchingExtension);
                }
                else
                {
                    UnloadExtension(matchingExtension);
                }
            }
            finally
            {
                await _hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extensionId);
            }
        }

        public async Task<string> PerformAction(Guid extensionId, string action, string parameters)
        {
            var matchingExtension = MatchingExtensionHost(extensionId);

            _logger.LogInformation($"Performing {action} for extension {matchingExtension.Name}");

            string result = await matchingExtension.Driver.PerformAction(action, parameters);

            _logger.LogInformation($"Saving configuration for extension {matchingExtension.Name}");

            await SaveCurrentConfiguration(matchingExtension);

            return result;
        }

        private ExtensionHost MatchingExtensionHost(Guid extensionId)
        {
            return _extensions.First(extension => extension.Id == extensionId);
        }

        public IControlPoint GetControlPoint(Guid extensionId, string endpointId)
        {
            var endpoints = Extensions.First(extension => extension.Id == extensionId).Driver.Endpoints;

            return endpoints.First(endpoint => endpoint.Id == endpointId) as IControlPoint;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task DiscoverExtensions()
        {
            var extensionFinder = new Finder<IHardwareDriver>();
            var assemblyPaths =
                extensionFinder.FindAssembliesWithPlugins(
                    Path.Combine(
                        Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? Environment.CurrentDirectory,
                        "Drivers"), _loggerFactory.CreateLogger<Finder<IHardwareDriver>>());

            foreach (string assemblyPath in assemblyPaths)
            {
                try
                {
                    await GetExtensionsFromAssembly(assemblyPath);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, $"Unable to discover assembly {assemblyPath}.");
                }
            }
        }

        private async Task GetExtensionsFromAssembly(string assemblyPath)
        {
            var host = new Host<IHardwareDriver>(assemblyPath);
            host.Load();

            foreach (var driver in host.GetExtensions())
            {
                var extension = await _extensionRepository.Get(driver.Id);
                if (extension == null)
                {
                    extension = new ExtensionHost
                        {Enabled = false, Id = driver.Id, Name = driver.Name};
                    await _extensionRepository.Insert(extension);
                }

                extension.AssemblyPath = assemblyPath;

                _extensions.Add(extension);
            }

            host.Unload();
        }

        private void LoadExtensions()
        {
            foreach (var extension in _extensions.Where(extension => extension.Enabled))
            {
                try
                {
                    LoadExtension(extension);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, $"Unable to load extension {extension.Name}");
                }
            }
        }

        private void LoadExtension(ExtensionHost extension)
        {
            extension.Host = new Host<IHardwareDriver>(extension.AssemblyPath);
            extension.Host.Load();
            extension.Driver = extension.Host.GetExtensions().First(ext => ext.Id == extension.Id);
            extension.Driver.UpdatedEndpoints += DriverOnUpdatedEndpoints;

            extension.Driver.Load(extension.Configuration, _loggerFactory);
            extension.Configuration = extension.Driver.CurrentConfiguration();

            extension.Loaded = true;
        }

        private async void DriverOnUpdatedEndpoints(object sender, EventArgs eventArgs)
        {
            if (!(sender is IHardwareDriver driver)) return;

            await EndpointUpdateSemaphore.WaitAsync();

            try
            {
                var existingEndpoints = (await _endpointRepository.GetForExtension(driver.Id)).ToArray();
                foreach (var endpoint in EndpointsToBeInserted(driver, existingEndpoints))
                {
                    var insertEndpoint = new Endpoint
                    {
                        EndpointId = endpoint.Id, ExtensionId = driver.Id, Name = endpoint.Name,
                        Type = endpoint is IControlPoint ? EndpointType.Output : EndpointType.Reader
                    };

                    await _endpointRepository.Insert(insertEndpoint);
                }

                foreach (var endpoint in EndpointsToBeDeleted(driver, existingEndpoints))
                {
                    await _endpointRepository.Delete(endpoint.Id);
                }

                await SaveCurrentConfiguration(MatchingExtensionHost(driver.Id));
            }
            finally
            {
                EndpointUpdateSemaphore.Release();
            }
        }

        private static IEnumerable<IEndpoint> EndpointsToBeInserted(IHardwareDriver driver,
            Endpoint[] existingEndpoints)
        {
            return driver.Endpoints.Where(endpoint =>
                endpoint.ExtensionId == driver.Id && !existingEndpoints
                    .Select(existingEndpoint => existingEndpoint.EndpointId).Contains(endpoint.Id));
        }

        private static IEnumerable<Endpoint> EndpointsToBeDeleted(IHardwareDriver driver,
            IEnumerable<Endpoint> existingEndpoints)
        {
            return existingEndpoints.Where(existingEndpoint =>
                existingEndpoint.ExtensionId == driver.Id && !driver.Endpoints
                    .Select(endpoint => endpoint.Id).Contains(existingEndpoint.EndpointId));
        }

        private async Task SaveCurrentConfiguration(ExtensionHost extension)
        {
            try
            {
                extension.Configuration = extension.Driver.CurrentConfiguration();
                await _extensionRepository.Update(extension);
            }
            finally
            {
                await _hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extension.Id);
            }
        }

        private void UnloadExtensions()
        {
            foreach (var extension in _extensions)
            {
                try
                {
                    UnloadExtension(extension);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, $"Unable to unload extension {extension.Name}");
                }
            }

            _extensions.Clear();
        }

        private void UnloadExtension(ExtensionHost extension)
        {
            extension.Driver.Unload();
            extension.Driver.UpdatedEndpoints -= DriverOnUpdatedEndpoints;
            
            extension.Host.Unload();
            extension.Loaded = false;
        }
    }
}