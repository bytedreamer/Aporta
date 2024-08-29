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
using AngleSharp.Dom;
using Aporta.Extensions.Endpoint;
using Aporta.Drivers.OSDP.Shared;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

namespace Aporta.Drivers.Virtual.WebClient.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ConfigurationTest : AportaTestContext
{
    private const string EmptyConfiguration = "{\"Readers\":[],\"Outputs\":[],\"Inputs\":[]}";

    private readonly Mock<IDriverConfigurationCalls> _mockConfigurationCalls = new();
    private readonly Mock<IDoorCalls> _mockDoorCalls = new();

    private readonly Guid _extensionId = Guid.NewGuid();

    private IRenderedComponent<Configuration>? _cut;
    
    public ConfigurationTest()
    {
        BlazoriseConfig.JSInterop.AddTextEdit(JSInterop);
        BlazoriseConfig.JSInterop.AddButton(JSInterop);

        Services.AddScoped<IDriverConfigurationCalls>(_ => _mockConfigurationCalls.Object);
    }

    private Shared.Configuration SetUpDeviceConfiguration(byte readerNumber)
    {
        var config = new Shared.Configuration();

        config.Readers.Add(new Shared.Reader { Name = "Virtual Reader 1", Number = readerNumber });
        config.Outputs.Add(new Shared.Output { Name = "Virtual Output 1", Number = readerNumber });
        config.Inputs.Add(new Shared.Input { Name = "Virtual Input 1", Number = readerNumber });

        return config;
    }

    private Shared.Configuration SetUpDeviceConfiguration(List<Shared.Reader> readers)
    {
        var config = new Shared.Configuration();

        foreach (var reader in readers)
        {
            config.Readers.Add(reader);
            config.Outputs.Add(new Shared.Output { Name = $"Virtual Output {reader.Number}", Number = reader.Number });
            config.Inputs.Add(new Shared.Input { Name = $"Virtual Input {reader.Number}", Number = reader.Number });
        }

        return config;
    }

    private void SetUpDoorMock()
    {
        Services.AddScoped<IDoorCalls>(_ => _mockDoorCalls.Object);
    }

    private void SetUpDoorMock(Endpoint[] endpoints)
    {        
        List<IEndpoint> _Iendpoints = new();

        _mockDoorCalls.Setup(calls => calls.GetAvailableEndpoints()).ReturnsAsync(endpoints);

        Services.AddScoped<IDoorCalls>(_ => _mockDoorCalls.Object);
    }

    [Test]
    public async Task SwipeBadge()
    {
        // Arrange

        SetUpDoorMock();

        byte readerNumber = 1;
        var cardData = "2468";
        var testConfiguration = JsonConvert.SerializeObject(SetUpDeviceConfiguration(readerNumber));

        var badgeSwipeParams = new BadgeSwipeAction
        {
            ReaderNumber = readerNumber,
            CardData = cardData
        };

        string badgeSwipeParamsSerialized = JsonConvert.SerializeObject(badgeSwipeParams);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, testConfiguration)
            .Add(p => p.ExtensionId, _extensionId)
            );

        var badgeSwipeShowModalButton = _cut.FindComponents<DropdownItem>().First(button => button.Nodes[0].TextContent.Trim() == "Swipe Badge");

        await _cut.InvokeAsync(async () => await badgeSwipeShowModalButton.Instance.Clicked.InvokeAsync());

        var textEdit = _cut.Find("#SwipeBadgeTextEdit");
        textEdit.Input(badgeSwipeParams.CardData);

        var swipeBadgeButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Swipe the badge");

        await _cut.InvokeAsync(async () => await swipeBadgeButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.BadgeSwipe.ToString(), JsonConvert.SerializeObject(badgeSwipeParams)));
    }

    [Test]
    public void ConfigurationComponentRendersCorrectly()
    {
        // Act - Render with an empty configuration
        SetUpDoorMock();

        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, EmptyConfiguration)
            .Add(p => p.ExtensionId, _extensionId));

        // Assert
        Assert.That(_cut.Nodes[0].TextContent, Does.Not.Contain("Setup"));
        Assert.That(_cut.Nodes[0].TextContent, Contains.Substring("Add Virtual Reader")); 

    }

    [Test]
    public async Task AddReader()
    {
        // Arrange
        SetUpDoorMock();

        byte readerNumber = 1;
        var config = SetUpDeviceConfiguration(readerNumber);
        var cardData = "2468";

        var newReaderName = "New Reader";

        var badgeSwipeParams = new BadgeSwipeAction
        {
            ReaderNumber = readerNumber,
            CardData = cardData

        };

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId));

        var addReaderButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Reader");

        await _cut.InvokeAsync(async () => await addReaderButton.Instance.Clicked.InvokeAsync());

        var textEdit = _cut.Find("#AddReaderTextEdit");
        textEdit.Input(newReaderName);

        var modalAddReaderButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add");

        await _cut.InvokeAsync(async () => await modalAddReaderButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddReader.ToString(), newReaderName));
    }

    [Test]
    public async Task RemoveReader()
    {
        // Arrange
        var readersOnRazorPage = new List<Shared.Reader>
        {
            new Shared.Reader{Name = "Virtual Reader 1",Number = 1 },
            new Shared.Reader{Name = "Virtual Reader 2",Number = 2 },
            new Shared.Reader{Name = "Virtual Reader 3",Number = 3 }
        };


        Endpoint[] availableEndPoints = {
            new Endpoint{ ExtensionId = _extensionId, Type = EndpointType.Reader, DriverEndpointId = $"VR{readersOnRazorPage[1].Number}", Id = readersOnRazorPage[1].Number },
            new Endpoint{ ExtensionId = _extensionId, Type = EndpointType.Reader, DriverEndpointId = $"VR{readersOnRazorPage[2].Number}", Id = readersOnRazorPage[2].Number }

        };

        SetUpDoorMock(availableEndPoints);

        var config = SetUpDeviceConfiguration(readersOnRazorPage);

        var readerToRemove = readersOnRazorPage[1];

        var confirmMessage = new Mock<IMessageService>();
        confirmMessage
            .Setup(confirm => confirm.Confirm(It.IsAny<string>(), "Delete reader", It.IsAny<Action<MessageOptions>>()))
            .ReturnsAsync(true);
        Services.AddSingleton(confirmMessage.Object);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId));

        var deleteButton = _cut.FindComponents<DropdownItem>().First(button => button.Nodes[0].TextContent.Trim() == "Delete");

        await _cut.InvokeAsync(async () => await deleteButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.RemoveReader.ToString(), JsonConvert.SerializeObject(readerToRemove)));
    }


}