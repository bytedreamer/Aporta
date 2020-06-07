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

namespace Aporta.Core.Services
{
    public interface IMainService
    {
        IEnumerable<ExtensionHost> Extensions { get; }

        Task Startup();

        void Shutdown();
    }
    
    public class MainService : IMainService
    {
        private readonly ExtensionRepository _extensionRepository;

        private readonly List<ExtensionHost> _extensions = new List<ExtensionHost>();

        public MainService(IDataAccess dataAccess)
        {
            _extensionRepository = new ExtensionRepository(dataAccess);
        }

        public IEnumerable<ExtensionHost> Extensions => _extensions;

        public async Task Startup()
        {
            await DiscoverExtensions();

            LoadExtensions();
        }

        public void Shutdown()
        {
            UnloadExtensions();
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
                var host = new Host<IHardwareDriver>(assemblyPath);
                host.Load();

                foreach (var driver in host.GetExtensions())
                {
                    try
                    {
                        driver.Load();
                    }
                    catch (Exception exception)
                    {
                        continue;
                    }

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

                foreach (var operation in host.GetExtensions())
                {
                    operation.Unload();
                }

                host.Unload();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void LoadExtensions()
        {
            foreach (var extension in _extensions.Where(extension => extension.Enabled))
            {
                extension.Host = new Host<IHardwareDriver>(extension.AssemblyPath);
                extension.Host.Load();
            }
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UnloadExtensions()
        {
            foreach (var extension in _extensions)
            {
                extension.Host.Unload();
            }

            _extensions.Clear();
        }
    }
}