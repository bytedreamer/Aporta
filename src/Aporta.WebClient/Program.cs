using System;
using System.Net.Http;
using System.Threading.Tasks;
using Aporta.Shared.Calls;
using Aporta.WebClient.Messaging;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Aporta.WebClient;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services
            .AddBlazorise( options =>
            {
                options.Immediate = true;
            } )
            .AddBootstrapProviders()
            .AddFontAwesomeIcons();

        builder.RootComponents.Add<App>("app");
        
        builder.Services.AddScoped<IDriverConfigurationCalls, DriverConfigurationCalls>(_ => new DriverConfigurationCalls(new HttpClient
            { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }));
        builder.Services.AddScoped<IExtensionCalls, ExtensionCalls>(_ => new ExtensionCalls(new HttpClient
            { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }));

        builder.Services.AddScoped<IDoorCalls, DoorCalls>(_ => new DoorCalls(new HttpClient
        { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }));

        builder.Services.AddScoped<IHubProxyFactory, HubProxyFactory>();
        
        builder.Services.AddScoped(_ => new HttpClient
            { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

        await builder.Build().RunAsync();
    }
}