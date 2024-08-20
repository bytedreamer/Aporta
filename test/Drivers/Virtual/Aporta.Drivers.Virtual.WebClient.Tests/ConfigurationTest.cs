using Aporta.Drivers.Virtual.Shared.Actions;
using Aporta.Drivers.Virtual.Shared;
using Aporta.Shared.Calls;
using Blazorise;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using TestWebClientConfiguration;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using AngleSharp.Diffing.Extensions;

namespace Aporta.Drivers.Virtual.WebClient.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ConfigurationTest : AportaTestContext
{
    private const string EmptyConfiguration = "{\"Readers\":[],\"Outputs\":[],\"Inputs\":[]}";

    private readonly Mock<IDriverConfigurationCalls> _mockConfigurationCalls = new();
    private readonly Mock<HttpClient> _mockHttpClient = new();


    private readonly Guid _extensionId = Guid.NewGuid();

    private IRenderedComponent<Configuration>? _cut;
    
    public ConfigurationTest()
    {
        BlazoriseConfig.JSInterop.AddTextEdit(JSInterop);
        BlazoriseConfig.JSInterop.AddButton(JSInterop);

        Services.AddScoped<IDriverConfigurationCalls>(_ => _mockConfigurationCalls.Object);

        Services.AddScoped(_ => new HttpClient
        { BaseAddress = new Uri("https://localhost:5001/") });

    }

    private Shared.Configuration SetUpDeviceConfiguration(byte readerNumber)
    {
        var config = new Shared.Configuration();

        config.Readers.Add(new Reader { Name = "Virtual Reader 1", Number = readerNumber });
        config.Outputs.Add(new Output { Name = "Virtual Output 1", Number = readerNumber });
        config.Inputs.Add(new Input { Name = "Virtual Input 1", Number = readerNumber });

        return config;
    }

    [Test]
    public async Task SwipeBadge()
    {
        // Arrange
        byte readerNumber = 1;
        var cardData = "2468";
        var testConfiguration = JsonConvert.SerializeObject(SetUpDeviceConfiguration(readerNumber));

        Dictionary<byte, string> badgeNumbers = new Dictionary<byte, string>() {
            {readerNumber, cardData }
         };

        string badgeSwipeParameters = JsonConvert.SerializeObject(new BadgeSwipeAction
        {            
            ReaderNumber = readerNumber,
            CardData = badgeNumbers[readerNumber]
            
        });

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, testConfiguration)
            .Add(p => p.ExtensionId, _extensionId)
            .Add(p => p.BadgeNumbers, badgeNumbers));

        var badgeSwipeButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Click to Simulate Badge Swipe");

        await _cut.InvokeAsync(async () => await badgeSwipeButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.BadgeSwipe.ToString(), badgeSwipeParameters));
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
        var testConfiguration = JsonConvert.SerializeObject(SetUpDeviceConfiguration(readerNumber));

        Dictionary<byte, string> badgeNumbers = new Dictionary<byte, string>() {
            {readerNumber, "2468" }
         };

        string addReaderParameters = JsonConvert.SerializeObject(new Reader
        {
            Name = "New Reader",
            Number = 4
        });

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, testConfiguration)
            .Add(p => p.ExtensionId, _extensionId)
            .Add(p => p.BadgeNumbers, badgeNumbers));

        var badgeSwipeButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Reader");

        await _cut.InvokeAsync(async () => await badgeSwipeButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddReader.ToString(), addReaderParameters));
    }


}