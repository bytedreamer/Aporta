using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aporta.WebClient.Tests;

public class AportaTestContext : Bunit.TestContext
{
    public AportaTestContext()
    {
        Services.AddBlazorise()
            .AddFontAwesomeIcons()
            .AddBootstrapProviders()
            .Replace(ServiceDescriptor.Transient<IComponentActivator, ComponentActivator>()); 
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        Services.AddSingleton(_ => new AportaRuntime(true));
    }
}
