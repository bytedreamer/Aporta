using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using RichardSzalay.MockHttp;

namespace Aporta.WebClient.Tests;

public class AportaTestContext : Bunit.TestContext
{
    protected readonly MockHttpMessageHandler Mock;

    public AportaTestContext()
    {
        Services.AddBlazorise()
            .AddFontAwesomeIcons()
            .AddBootstrapProviders()
            .Replace(ServiceDescriptor.Transient<IComponentActivator, ComponentActivator>()); 
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        Mock = Services.AddMockHttpClient();
        Services.AddSingleton(_ => new AportaRuntime(true));
    }
}
