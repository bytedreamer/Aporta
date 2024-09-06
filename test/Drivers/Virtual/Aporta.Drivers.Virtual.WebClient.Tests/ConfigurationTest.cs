using Aporta.Drivers.Virtual.Shared.Actions;
using Aporta.Shared.Calls;
using Blazorise;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using TestWebClientConfiguration;
using Aporta.Shared.Models;

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
        Assert.That(_cut.Nodes[0].TextContent, Contains.Substring("Add Virtual Input"));
        Assert.That(_cut.Nodes[0].TextContent, Contains.Substring("Add Virtual Output"));

    }

    [Test]
    public async Task AddReader()
    {
        // Arrange
        SetUpDoorMock();

        byte readerNumber = 1;
        var config = SetUpDeviceConfiguration(readerNumber);

        var newReader = new Shared.AddReaderParameter { Name = "New Reader" };

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId));

        var addReaderButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Reader");

        await _cut.InvokeAsync(async () => await addReaderButton.Instance.Clicked.InvokeAsync());

        var textEdit = _cut.Find("#AddReaderTextEdit");
        textEdit.Input(newReader.Name);

        var modalAddReaderButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add");

        await _cut.InvokeAsync(async () => await modalAddReaderButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddReader.ToString(), JsonConvert.SerializeObject(newReader)));
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
            new() { ExtensionId = _extensionId, Type = EndpointType.Reader, DriverEndpointId = $"VR{readersOnRazorPage[1].Number}", Id = readersOnRazorPage[1].Number },
            new() { ExtensionId = _extensionId, Type = EndpointType.Reader, DriverEndpointId = $"VR{readersOnRazorPage[2].Number}", Id = readersOnRazorPage[2].Number }

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

    [Test]
    public async Task AddInput()
    {
        // Arrange
        SetUpDoorMock();

        var emptyConfig = new Shared.Configuration();

        var newInput = new Shared.AddInputParameter { Name = "Test Input" };

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(emptyConfig))
            .Add(p => p.ExtensionId, _extensionId));

        var addInputButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Input");

        await _cut.InvokeAsync(async () => await addInputButton.Instance.Clicked.InvokeAsync());

        var textEdit = _cut.Find("#AddInputTextEdit");
        textEdit.Input(newInput.Name);

        var modalAddInputButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add");

        await _cut.InvokeAsync(async () => await modalAddInputButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddInput.ToString(), JsonConvert.SerializeObject(newInput)));
    }


    [Test]
    public async Task RemoveInput()
    {
        // Arrange
        var inputsOnRazorPage = new List<Shared.Input>
        {
            new Shared.Input{Name = "Virtual Input 1",Number = 1 },
            new Shared.Input{Name = "Virtual Input 2",Number = 2 },
            new Shared.Input{Name = "Virtual Input 3",Number = 3 }
        };

        //set up endpoints not assigned to a door
        Endpoint[] availableEndPoints = {
            new() { ExtensionId = _extensionId, Type = EndpointType.Reader, DriverEndpointId = $"VI{inputsOnRazorPage[1].Number}", Id = inputsOnRazorPage[1].Number },
            new() { ExtensionId = _extensionId, Type = EndpointType.Reader, DriverEndpointId = $"VI{inputsOnRazorPage[2].Number}", Id = inputsOnRazorPage[2].Number }

        };

        SetUpDoorMock(availableEndPoints);

        var config = new Shared.Configuration();

        config.Inputs.AddRange(inputsOnRazorPage);

        var inputToRemove = inputsOnRazorPage[1];

        var confirmMessage = new Mock<IMessageService>();
        confirmMessage
            .Setup(confirm => confirm.Confirm(It.IsAny<string>(), "Delete input", It.IsAny<Action<MessageOptions>>()))
            .ReturnsAsync(true);
        Services.AddSingleton(confirmMessage.Object);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId));

        var deleteButton = _cut.FindComponents<DropdownItem>().First(button => button.Nodes[0].TextContent.Trim() == "Delete");

        await _cut.InvokeAsync(async () => await deleteButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.RemoveInput.ToString(), JsonConvert.SerializeObject(inputToRemove)));
    }

    [Test]
    public async Task AddOutput()
    {
        // Arrange
        SetUpDoorMock();

        var emptyConfig = new Shared.Configuration();

        var newOutput = new Shared.AddInputParameter { Name = "Test Output" };

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(emptyConfig))
            .Add(p => p.ExtensionId, _extensionId));

        var addOutputButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Output");

        await _cut.InvokeAsync(async () => await addOutputButton.Instance.Clicked.InvokeAsync());

        var textEdit = _cut.Find("#AddOutputTextEdit");
        textEdit.Input(newOutput.Name);

        var modalAddOutputButton = _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add");

        await _cut.InvokeAsync(async () => await modalAddOutputButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddOutput.ToString(), JsonConvert.SerializeObject(newOutput)));
    }

    [Test]
    public async Task RemoveOutput()
    {
        // Arrange
        var outputsOnRazorPage = new List<Shared.Output>
        {
            new Shared.Output{Name = "Virtual Output 1",Number = 1 },
            new Shared.Output{Name = "Virtual Output 2",Number = 2 },
            new Shared.Output{Name = "Virtual Output 3",Number = 3 }
        };

        //set up endpoints not assigned to a door
        Endpoint[] availableEndPoints = {
            new() { ExtensionId = _extensionId, Type = EndpointType.Reader, DriverEndpointId = $"VO{outputsOnRazorPage[1].Number}", Id = outputsOnRazorPage[1].Number },
            new() { ExtensionId = _extensionId, Type = EndpointType.Reader, DriverEndpointId = $"VO{outputsOnRazorPage[2].Number}", Id = outputsOnRazorPage[2].Number }

        };

        SetUpDoorMock(availableEndPoints);

        var config = new Shared.Configuration();

        config.Outputs.AddRange(outputsOnRazorPage);

        var outputToRemove = outputsOnRazorPage[1];

        var confirmMessage = new Mock<IMessageService>();
        confirmMessage
            .Setup(confirm => confirm.Confirm(It.IsAny<string>(), "Delete output", It.IsAny<Action<MessageOptions>>()))
            .ReturnsAsync(true);
        Services.AddSingleton(confirmMessage.Object);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId));

        var deleteButton = _cut.FindComponents<DropdownItem>().First(button => button.Nodes[0].TextContent.Trim() == "Delete");

        await _cut.InvokeAsync(async () => await deleteButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.RemoveOutput.ToString(), JsonConvert.SerializeObject(outputToRemove)));
    }

}