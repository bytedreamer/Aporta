using System;
using System.Net.Http;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Aporta.WebClient
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            
            builder.Services
                .AddBlazorise()
                .AddBootstrapProviders()
                .AddFontAwesomeIcons();

            builder.Services.AddSingleton( new HttpClient
            {
                BaseAddress = new Uri( builder.HostEnvironment.BaseAddress )
            } );
            
            builder.RootComponents.Add<App>("app");

            builder.Services.AddTransient(_ => new HttpClient
                {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});

            var host = builder.Build();

            await host.RunAsync();
        }
    }
}