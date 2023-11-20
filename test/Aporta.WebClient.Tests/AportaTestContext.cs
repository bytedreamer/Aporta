using Aporta.WebClient.Hubs;

using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Blazorise.Modules;
using Blazorise.Snackbar;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aporta.WebClient.Tests;

public class AportaTestContext : Bunit.TestContext
{
    private readonly Mock<IHubProxy> _hubProxyMock = new();
    
    protected AportaTestContext()
    {
        Services.AddBlazorise()
            .AddFontAwesomeIcons()
            .AddBootstrapProviders()
            .Replace(ServiceDescriptor.Transient<IComponentActivator, ComponentActivator>());
       BlazoriseConfig.AddBootstrapProviders(Services);
       BlazoriseConfig.JSInterop.AddUtilities(JSInterop);
        
        Mock<IHubProxyFactory> hubProxyFactoryMock = new();
        hubProxyFactoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(_hubProxyMock.Object);
        Services.AddScoped<IHubProxyFactory>(_ => hubProxyFactoryMock.Object);
    }
}
