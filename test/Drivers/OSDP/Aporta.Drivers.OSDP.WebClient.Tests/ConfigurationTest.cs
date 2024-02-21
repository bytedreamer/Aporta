using Blazorise;
using Bunit;
using TestWebClientConfiguration;

namespace Aporta.Drivers.OSDP.WebClient.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ConfigurationTest : AportaTestContext
{
    private const string EmptyConfiguration = "{\"AvailablePorts\":[],\"Buses\":[]}";
    private const string TestConfiguration = "{\"AvailablePorts\":[\"COM4\",\"COM3\"],\"Buses\":[{\"PortName\":\"COM3\",\"BaudRate\":9600,\"Devices\":[]}]}";
    
    private IRenderedComponent<Configuration>? _cut;
    
    public ConfigurationTest()
    {
        BlazoriseConfig.JSInterop.AddTextEdit(JSInterop);
        BlazoriseConfig.JSInterop.AddButton(JSInterop);
    }

    [Test]
    public void ConfigurationComponentRendersCorrectly()
    {
        // Act - Render with an empty configuration
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, EmptyConfiguration));

        // Assert
        Assert.That(_cut.FindComponent<AlertDescription>().Nodes[0].TextContent, Is.EqualTo("None are available"));
    }
}