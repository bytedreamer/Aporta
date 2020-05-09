using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Aporta.Core.Extension;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AportaConsole
{
    class Program
    {
        private static ILoggerFactory _loggerFactory;
        
        static void Main(string[] args)
        {
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger(); 
            
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddSerilog(serilogLogger);
            
            TestOutput();
            
            GC.Collect();
            GC.WaitForPendingFinalizers(); 
            
            Console.ReadKey();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TestOutput()
        {
            var extensionFinder = new Finder<IHardwareDriver>();
            var assemblyPaths = extensionFinder.FindAssembliesWithPlugins(Path.Combine(Directory.GetCurrentDirectory(), "Drivers"));

            var host = new Host<IHardwareDriver>(assemblyPaths.First());
            host.Load();
        
            foreach (var operation in host.GetExtensions())
            {
                operation.Load();
            }
            
            Console.ReadKey();
            
            foreach (var operation in host.GetExtensions())
            {
                operation.Unload();
            }
            
            host.Unload();
        }
    }
}
