using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Aporta.Core.Extension;

internal class AportaAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    internal const string ExtensionsName = "Aporta.Extensions";
        
    public AportaAssemblyLoadContext(string mainAssemblyToLoadPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyToLoadPath);
    }
        
    protected override Assembly Load(AssemblyName name)
    {
        // Keep host version of shared assemblies
        if (!name.Name?.StartsWith("Aporta") ?? false)
        {
            try
            {
                return Default.LoadFromAssemblyName(name);
            }
            catch
            {
                // Ignore - probably a plugin dependency
            }
        }
            
        if (name.Name == ExtensionsName)
        {
            return null;
        }
            
        string assemblyPath = _resolver.ResolveAssemblyToPath(name);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
        
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }
}