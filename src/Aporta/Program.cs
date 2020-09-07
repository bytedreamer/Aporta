using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Aporta.Workers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aporta
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var host = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? CreateWindowsHostBuilder(args).Build()
                : CreateSystemdHostBuilder(args).Build();

            await host.RunAsync();
        }

        public static IHostBuilder CreateSystemdHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<StartupWorker>();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        public static IHostBuilder CreateWindowsHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<StartupWorker>();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
