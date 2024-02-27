using Aporta.Drivers.OSDP.Shared;
using Aporta.Drivers.OSDP.Shared.Actions;
using Aporta.Shared.Calls;
using Blazorise;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using TestWebClientConfiguration;

namespace Aporta.Drivers.OSDP.WebClient.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ConfigurationTest : AportaTestContext
{
    private const string EmptyConfiguration = "{\"AvailablePorts\":[],\"Buses\":[]}";
    private const string TestConfiguration = "{\"AvailablePorts\":[\"COM4\",\"COM3\"],\"Buses\":[{\"PortName\":\"COM3\",\"BaudRate\":9600,\"Devices\":[]}]}";
    
    private readonly Mock<IDriverConfigurationCalls> _mockConfigurationCalls = new();

    private readonly Guid _extensionId = new();
    
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
            .Add(p => p.RawConfiguration, EmptyConfiguration)
            .Add(p => p.ExtensionId, _extensionId));

        // Assert
        Assert.That(_cut.FindComponent<AlertDescription>().Nodes[0].TextContent, Is.EqualTo("None are available"));
    }
    
    [Test]
    public async Task AddBus()
    {
        // Arrange
        string busActionParameters = JsonConvert.SerializeObject(new BusAction
        {
            Bus = new Bus{BaudRate = 9600, PortName = "COM4"}
        });
        _mockConfigurationCalls.Setup(calls => calls.PerformAction(_extensionId, "AddBus", busActionParameters)).Verifiable();
        
        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, TestConfiguration)
            .Add(p => p.ExtensionId, _extensionId));

        var addBusButton = _cut.FindComponents<Button>().Single(button => button.Nodes[0].TextContent == "Add RS-485 Port");
        await _cut.InvokeAsync(async () => await addBusButton.Instance.Clicked.InvokeAsync());

        var modal = _cut.FindComponents<Modal>().First(modal => modal.Instance.ClassNames.Contains("show"));
        var modalAddButton = modal.FindComponents<Button>().Single(button => button.Nodes[0].TextContent == "Add");
        await _cut.InvokeAsync(async () => await modalAddButton.Instance.Clicked.InvokeAsync());
        
        // Assert
        _mockConfigurationCalls.Verify();
    }
}