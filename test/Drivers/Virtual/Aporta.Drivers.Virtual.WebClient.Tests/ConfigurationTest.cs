using Aporta.Drivers.Virtual.Shared.Actions;
using Aporta.Drivers.Virtual.Shared;
using Aporta.Shared.Calls;
using Blazorise;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using TestWebClientConfiguration;
using Aporta.Shared.Models;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using AngleSharp.Diffing.Extensions;

namespace Aporta.Drivers.Virtual.WebClient.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ConfigurationTest : AportaTestContext
{
    private const string EmptyConfiguration = "{\"Readers\":[],\"Outputs\":[],\"Inputs\":[]}";

    private readonly Mock<IDriverConfigurationCalls> _mockConfigurationCalls = new();
    private readonly Mock<IDoorCalls> _mockDoorCalls = new();

    
    private readonly Mock<IExtensionCalls> _mockExtensionCalls = new();


    private readonly Guid _extensionId = Guid.NewGuid();

    private IRenderedComponent<Configuration>? _cut;
    
    public ConfigurationTest()
    {
        BlazoriseConfig.JSInterop.AddTextEdit(JSInterop);
        BlazoriseConfig.JSInterop.AddButton(JSInterop);

        Services.AddScoped<IDriverConfigurationCalls>(_ => _mockConfigurationCalls.Object);
        Services.AddScoped<IDoorCalls>(_ => _mockDoorCalls.Object);

        byte readerNumber = 1;
        var cardData = "2468";
        var testConfiguration = JsonConvert.SerializeObject(SetUpDeviceConfiguration(readerNumber));

        _mockExtensionCalls.Setup(calls => calls.GetExtension(_extensionId)).ReturnsAsync(new Extension() { Id = _extensionId, Configuration = testConfiguration });

        Services.AddScoped<IExtensionCalls>(_ => _mockExtensionCalls.Object);
    }

    private Shared.Configuration SetUpDeviceConfiguration(byte readerNumber)
    {
        var config = new Shared.Configuration();

        config.Readers.Add(new Reader { Name = "Virtual Reader 1", Number = readerNumber });
        config.Outputs.Add(new Shared.Output { Name = "Virtual Output 1", Number = readerNumber });
        config.Inputs.Add(new Shared.Input { Name = "Virtual Input 1", Number = readerNumber });

        return config;
    }

    [Test]
    public async Task SwipeBadge()
    {
        // Arrange
        byte readerNumber = 1;
        var cardData = "2468";
        var testConfiguration = JsonConvert.SerializeObject(SetUpDeviceConfiguration(readerNumber));

        var badgeSwipeParams = new BadgeSwipeAction
        {
            ReaderNumber = readerNumber,
            CardData = cardData

        };

        var newReader = new Reader
        {
            Name = "New Reader",
            Number = 0
        };

        string badgeSwipeParamsSerialized = JsonConvert.SerializeObject(badgeSwipeParams);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, testConfiguration)
            .Add(p => p.ExtensionId, _extensionId)
            //.Add(p => p.BadgeSwipeActionSerialized, JsonConvert.SerializeObject(badgeSwipeParams))
            //.Add(p => p.ReaderToAddSerialized, JsonConvert.SerializeObject(newReader))
            );

        var badgeSwipeButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Click to Simulate Badge Swipe");

        await _cut.InvokeAsync(async () => await badgeSwipeButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.BadgeSwipe.ToString(), JsonConvert.SerializeObject(badgeSwipeParams)));
    }

    [Test]
    public void ConfigurationComponentRendersCorrectly()
    {
        // Act - Render with an empty configuration
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, EmptyConfiguration));

        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, EmptyConfiguration)
            .Add(p => p.ExtensionId, _extensionId));

        // Assert
        Assert.That(_cut.Nodes[0].TextContent, Does.Not.Contain("Setup"));
        Assert.That(_cut.Nodes[0].TextContent, Contains.Substring("Reader Name")); 

    }

    [Test]
    public async Task AddReader()
    {
        // Arrange
        byte readerNumber = 1;
        var config = SetUpDeviceConfiguration(readerNumber);
        var cardData = "2468";

        var newReader = new Reader
        {
            Name = "New Reader",
            Number = 0
        };

        var badgeSwipeParams = new BadgeSwipeAction
        {
            ReaderNumber = readerNumber,
            CardData = cardData

        };

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId)
            //.Add(p => p.BadgeSwipeActionSerialized, JsonConvert.SerializeObject(badgeSwipeParams))
            //.Add(p => p.ReaderToAddSerialized, JsonConvert.SerializeObject(newReader))
            );

        var addReaderButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Reader");

        await _cut.InvokeAsync(async () => await addReaderButton.Instance.Clicked.InvokeAsync());

        var modalAddReaderButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add");

        await _cut.InvokeAsync(async () => await modalAddReaderButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddReader.ToString(), JsonConvert.SerializeObject(newReader)));
    }


}