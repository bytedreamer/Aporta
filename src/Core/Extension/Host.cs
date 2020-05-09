using System;
using System.Collections.Generic;
using Aporta.Extensions;

namespace Aporta.Core.Extension
{
    public class Host<TExtension> where TExtension : IExtension
    {
        private readonly string _assemblyPath;
        private readonly Dictionary<string, TExtension> _extensions = new Dictionary<string, TExtension>();
        private readonly AportaAssemblyLoadContext _assemblyLoadingContext;

        public Host(string assemblyPath)
        {
            _assemblyPath = assemblyPath;
            _assemblyLoadingContext = new AportaAssemblyLoadContext(assemblyPath);
        }
        
        public IEnumerable<TExtension> GetExtensions()
        {
            return _extensions.Values;
        }

        public void Load()
        {
            foreach (var pluginType in Finder<TExtension>.GetExtensionTypes(
                _assemblyLoadingContext.LoadFromAssemblyPath(_assemblyPath)))
            {
                RegisterExtension((TExtension) Activator.CreateInstance(pluginType));
            }
        }

        private void RegisterExtension(TExtension pluginInstance)
        {
            _extensions.Add(pluginInstance.Name, pluginInstance);
        }

        public void Unload()
        {
            _extensions.Clear();
            _assemblyLoadingContext.Unload();
        }
    }
}