using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using Aporta.Core.DataAccess;
using Aporta.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aporta
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            
            using (var serviceScope = host.Services.CreateScope())
            {
                var services = serviceScope.ServiceProvider;

                try
                {
                    var dataAccess = services.GetRequiredService<IDataAccess>();
                    await dataAccess.UpdateSchema();
                    
                    var extensionService = services.GetRequiredService<ExtensionService>();
                    await extensionService.Startup();

                    services.GetRequiredService<AccessService>();
                    services.GetRequiredService<InputService>();
                    services.GetRequiredService<OutputService>();
                }
                catch (Exception exception)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(exception, "An error occurred.");
                }
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
