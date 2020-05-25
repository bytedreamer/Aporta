using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Aporta.Core.Extension;
using Aporta.Extensions.Endpoint;
using Aporta.Extensions.Hardware;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AportaConsole
{
    class Program
    {
        private static ILoggerFactory _loggerFactory;
        
        static async Task Main(string[] args)
        {
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger(); 
            
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddSerilog(serilogLogger);
            
            await TestOutput();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task TestOutput()
        {
            var extensionFinder = new Finder<IHardwareDriver>();
            var assemblyPaths = extensionFinder.FindAssembliesWithPlugins(Path.Combine(Directory.GetCurrentDirectory(), "Drivers"));

            var host = new Host<IHardwareDriver>(assemblyPaths.First());
            host.Load();
        
            foreach (var operation in host.GetExtensions())
            {
                try
                {
                    operation.Load();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }

                if (!operation.Endpoints.Any()) continue;
                
                await operation.Endpoints.OfType<IControlPoint>().First().Set(true);
                await Task.Delay(TimeSpan.FromSeconds(1));
                await operation.Endpoints.OfType<IControlPoint>().First().Set(false);
            }
            
            Console.WriteLine("Press any key to unload");
            Console.ReadKey();
            
            foreach (var operation in host.GetExtensions())
            {
                operation.Unload();
            }
            
            host.Unload();
        }
    }
}
