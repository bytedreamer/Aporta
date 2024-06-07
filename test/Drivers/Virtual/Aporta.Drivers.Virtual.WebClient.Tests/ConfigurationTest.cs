using Aporta.Shared.Calls;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TestWebClientConfiguration;

namespace Aporta.Drivers.Virtual.WebClient.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ConfigurationTest : AportaTestContext
{
    private const string EmptyConfiguration = "{\"Readers\":[],\"Outputs\":[],\"Inputs\":[]}";
    
    private readonly Mock<IDriverConfigurationCalls> _mockConfigurationCalls = new();
    
    private IRenderedComponent<Configuration>? _cut;
    
    public ConfigurationTest()
    {
        BlazoriseConfig.JSInterop.AddTextEdit(JSInterop);
        BlazoriseConfig.JSInterop.AddButton(JSInterop);
        
        Services.AddScoped<IDriverConfigurationCalls>(_ => _mockConfigurationCalls.Object);
    }

    [Test]
    public void ConfigurationComponentRendersCorrectly()
    {
        // Act - Render with an empty configuration
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, EmptyConfiguration));

        // Assert
        Assert.That(_cut.Nodes[0].TextContent, Does.Not.Contain("Setup"));
        Assert.That(_cut.Nodes[0].TextContent, Contains.Substring("Reader Name")); 

    }
  
}