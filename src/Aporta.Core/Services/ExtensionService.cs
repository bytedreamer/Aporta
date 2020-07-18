using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ExtensionService> _logger;
        private readonly ExtensionRepository _extensionRepository;
        private readonly EndpointRepository _endpointRepository;
        private readonly IHubContext<DataChangeNotificationHub> _hubContext;

        private readonly List<ExtensionHost> _extensions = new List<ExtensionHost>();
        
        private readonly List<IControlPoint> _configuredEndpoints = new List<IControlPoint>();

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

            string result =  await matchingExtension.Driver.PerformAction(action, parameters);
            
            _logger.LogInformation($"Saving configuration for extension {matchingExtension.Name}");

            try
            {
                matchingExtension.Configuration = matchingExtension.Driver.CurrentConfiguration();
                await _extensionRepository.Update(matchingExtension);
            }
            finally
            {
                await _hubContext.Clients.All.SendAsync(Methods.ExtensionDataChanged, extensionId);
            }

            return result;
        }
        
        private ExtensionHost MatchingExtensionHost(Guid extensionId)
        {
            return _extensions.First(extension => extension.Id == extensionId);
        }

        public IControlPoint GetControlPoint(Guid extensionId, string driverId)
        {
            return _configuredEndpoints.First(endpoint =>
                endpoint.ExtensionId == extensionId && endpoint.Id == driverId);
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
                catch(Exception exception)
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
            extension.Driver.AddEndpoints += DriverOnAddEndpoints;
            
            extension.Driver.Load(extension.Configuration, _loggerFactory);
            extension.Configuration = extension.Driver.CurrentConfiguration();

            extension.Loaded = true;
        }

        private async void DriverOnAddEndpoints(object sender, AddEndpointsEventArgs eventArgs)
        {
            if (!(sender is IHardwareDriver driver)) return;

            var existingEndpoints = (await _endpointRepository.GetForExtension(driver.Id)).ToArray();
            foreach (var endpoint in eventArgs.Endpoints.Cast<IControlPoint>())
            {
                if (!existingEndpoints.Any(configuredEndpoint =>
                    configuredEndpoint.ExtensionId == driver.Id && configuredEndpoint.DriverId != endpoint.Id))
                {
                    var configuredEndpoint = new Endpoint
                    {
                        DriverId = endpoint.Id, ExtensionId = driver.Id, Name = endpoint.Name,
                        Type = EndpointType.Output
                    };

                    await _endpointRepository.Insert(configuredEndpoint);
                }
                _configuredEndpoints.Add(endpoint);
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
            extension.Driver.AddEndpoints -= DriverOnAddEndpoints;
            
            extension.Host.Unload();
            extension.Loaded = false;
        }
    }
}