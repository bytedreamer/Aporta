using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aporta.Extensions;

namespace Aporta.Core.Extension
{
    public class Finder<TExtension> where TExtension : IExtension
    {
        private const string AssemblySearchPattern = "Aporta.*.dll";

        public IEnumerable<string> FindAssembliesWithPlugins(string path)
        {
            return FindExtensionsInAssemblies(AssemblyPaths(path).Where(assemblyPath =>
                !assemblyPath.EndsWith($"{AportaAssemblyLoadContext.ExtensionsName}.dll")));
        }

        private static IEnumerable<string> AssemblyPaths(string path)
        {
            return Directory.GetFiles(path, AssemblySearchPattern,
                new EnumerationOptions {RecurseSubdirectories = true});
        }

        private static IEnumerable<string> FindExtensionsInAssemblies(IEnumerable<string> assemblyPaths)
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