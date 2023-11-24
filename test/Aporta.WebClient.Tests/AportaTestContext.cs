using Aporta.WebClient.Messaging;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aporta.WebClient.Tests;

public class AportaTestContext : Bunit.TestContext
{
    protected readonly Mock<IHubProxy> HubProxyMock = new();
    
    protected AportaTestContext()
    {
        Services.AddBlazorise()
            .AddFontAwesomeIcons()
            .AddBootstrapProviders()
            .Replace(ServiceDescriptor.Transient<IComponentActivator, ComponentActivator>());
       BlazoriseConfig.AddBootstrapProviders(Services);
       BlazoriseConfig.JSInterop.AddUtilities(JSInterop);
        
        Mock<IHubProxyFactory> hubProxyFactoryMock = new();
        hubProxyFactoryMock.Setup(x => x.Create(It.IsAny<Uri>())).Returns(HubProxyMock.Object);
        Services.AddScoped<IHubProxyFactory>(_ => hubProxyFactoryMock.Object);
    }
}
