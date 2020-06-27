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
using Aporta.Core.Models;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;

namespace Aporta.Core.Services
{
    /// <summary>
    /// 
    /// </summary>
    public class MainService : IMainService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<MainService> _logger;
        private readonly ExtensionRepository _extensionRepository;

        private readonly List<ExtensionHost> _extensions = new List<ExtensionHost>();

        public MainService(IDataAccess dataAccess, ILogger<MainService> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _extensionRepository = new ExtensionRepository(dataAccess);
        }

        public IEnumerable<ExtensionHost> Extensions => _extensions;

        public async Task Startup()
        {
            _logger.LogInformation("Starting main service");
            
            await DiscoverExtensions();

            LoadExtensions();
        }

        public void Shutdown()
        {
            _logger.LogInformation("Shutting down main service");
            
            UnloadExtensions();
        }

        public async Task EnableExtension(Guid extensionId, bool enabled)
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

        public IHardwareDriver Driver(Guid extensionId)
        {
            return _extensions.First(extension => extension.Id == extensionId).Driver;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task DiscoverExtensions()
        {
            var extensionFinder = new Finder<IHardwareDriver>();
            var assemblyPaths =
                extensionFinder.FindAssembliesWithPlugins(
                    Path.Combine(
                        Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? Environment.CurrentDirectory,
                        "Drivers"));

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
            extension.Driver.Load(extension.Configuration, _loggerFactory);
            extension.Configuration = extension.Driver.InitialSettings();
            
            extension.Loaded = true;
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

        private static void UnloadExtension(ExtensionHost extension)
        {
            extension.Driver.Unload();
            
            extension.Host.Unload();
            extension.Loaded = false;
        }
    }
}