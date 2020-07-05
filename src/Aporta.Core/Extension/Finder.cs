using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aporta.Extensions;
using Microsoft.Extensions.Logging;

namespace Aporta.Core.Extension
{
    public class Finder<TExtension> where TExtension : IExtension
    {
        private const string AssemblySearchPattern = "Aporta.*.dll";
        
        public IEnumerable<string> FindAssembliesWithPlugins(string path, ILogger<Finder<TExtension>> logger)
        {
            return FindExtensionsInAssemblies(AssemblyPaths(path).Where(assemblyPath =>
                !assemblyPath.EndsWith($"{AportaAssemblyLoadContext.ExtensionsName}.dll")), logger);
        }

        private static IEnumerable<string> AssemblyPaths(string path)
        {
            return Directory.GetFiles(path, AssemblySearchPattern,
                new EnumerationOptions {RecurseSubdirectories = true});
        }

        private static IEnumerable<string> FindExtensionsInAssemblies(IEnumerable<string> assemblyPaths, ILogger<Finder<TExtension>> logger)
        {
            var extensionAssemblyLocations = new List<string>();

            foreach (var assemblyPath in assemblyPaths)
            {
                try
                {
                    var assemblyContext = new AportaAssemblyLoadContext(assemblyPath);

                    var assembly = assemblyContext.LoadFromAssemblyPath(assemblyPath);
                    if (GetExtensionTypes(assembly).Any())
                    {
                        extensionAssemblyLocations.Add(assembly.Location);
                    }

                    assemblyContext.Unload();
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, $"Unable to load assembly {assemblyPath}");
                }
            }

            return extensionAssemblyLocations;
        }

        public static IEnumerable<Type> GetExtensionTypes(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(type =>
                    !type.IsAbstract &&
                    typeof(TExtension).IsAssignableFrom(type));
        }
    }
}