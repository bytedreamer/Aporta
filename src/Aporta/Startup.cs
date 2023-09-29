using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Aporta.Core.DataAccess;
using Aporta.Core.Hubs;
using Aporta.Core.Services;
using Aporta.Shared.Messaging;
using Aporta.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aporta;

public class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(
            Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? Environment.CurrentDirectory, "Data")));
        services.AddSingleton<IDataEncryption, DataEncryptor>();

        services.AddSingleton<IDataAccess, SqLiteDataAccess>();

        services.AddSingleton<AccessService, AccessService>();
        services.AddSingleton<CredentialService, CredentialService>();
        services.AddSingleton<DoorConfigurationService, DoorConfigurationService>();
        services.AddSingleton<EventService, EventService>();
        services.AddSingleton<ExtensionService, ExtensionService>();
        services.AddSingleton<GlobalSettingService, GlobalSettingService>();
        services.AddSingleton<InputService, InputService>();
        services.AddSingleton<OutputService, OutputService>();
        services.AddSingleton<PeopleService, PeopleService>();

        services.AddSignalR();
        services.AddControllersWithViews();
        services.AddRazorPages();
        services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                new[] {"application/octet-stream"});
        });

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddLogging(loggingBuilder => {
                loggingBuilder.AddFile("/var/log/aporta.log", options =>
                {
                    options.Append = true;
                    options.MaxRollingFiles = 10;
                    options.FileSizeLimitBytes = 1000000;
                });
            });
        }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseResponseCompression();
            
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.UseRouting();
            
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapControllers();
                
            endpoints.MapFallbackToFile("index.html");
                
            endpoints.MapHub<DataChangeNotificationHub>(Locations.DataChangeNotification);
        });
    }
}