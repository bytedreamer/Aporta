using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Aporta.Core.Services;
using Aporta.Utilities;
using Aporta.Workers;

namespace Aporta;

class Program
{
    public static async Task Main(string[] args)
    {
        var host = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? CreateWindowsHostBuilder(args).Build()
            : CreateSystemdHostBuilder(args).Build();

        await host.RunAsync();
    }

    private static IHostBuilder CreateSystemdHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSystemd()
            .ConfigureServices((_, services) =>
            {
                services.AddHostedService<StartupWorker>();
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.ConfigureServices(ConfigureServices);
            });

    private static IHostBuilder CreateWindowsHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureServices((_, services) =>
            {
                services.AddHostedService<StartupWorker>();
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.ConfigureServices(ConfigureServices);
            });
        
    private static void ConfigureServices(IServiceCollection services)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            // ReSharper disable once AsyncVoidLambda
            options.ConfigureHttpsDefaults(async listenOptions =>
            {
                var globalSettingService =
                    ActivatorUtilities.CreateInstance<GlobalSettingService>(options.ApplicationServices);

                string password = await globalSettingService.GetSslCertificatePassword();
                if (!SslCertificateUtilities.IsThereAValidCertificate(password))
                {
                    var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
                    ILogger logger = loggerFactory.CreateLogger<Program>();
                    logger.LogWarning("A valid SSL certificate not found, auto creating a self-sign certificate");

                    SslCertificateUtilities.CreateAndSaveSelfSignedServerCertificate(password);
                }

                listenOptions.ServerCertificate = SslCertificateUtilities.LoadCertificate(password);
            });
        });
    }
}