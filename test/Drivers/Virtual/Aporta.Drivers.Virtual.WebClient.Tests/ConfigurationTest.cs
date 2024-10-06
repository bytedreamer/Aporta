using Aporta.Drivers.Virtual.Shared;
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

    private Shared.Configuration SetUpDeviceConfiguration()
    {
        var config = new Shared.Configuration();

        config.Readers.Add(new Device { Name = "Virtual Reader 1", Number = 1 });
        config.Outputs.Add(new Device { Name = "Virtual Output 1", Number = 1 });
        config.Inputs.Add(new Device { Name = "Virtual Input 1", Number = 1 });

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
        var testConfiguration = JsonConvert.SerializeObject(SetUpDeviceConfiguration());

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
        Assert.That(_cut.Nodes[1].TextContent, Contains.Substring("Add Virtual Input"));
        Assert.That(_cut.Nodes[2].TextContent, Contains.Substring("Add Virtual Output"));
    }

    [Test]
    public async Task AddReader()
    {
        // Arrange
        SetUpDoorMock();
        
        var emptyConfig = new Shared.Configuration();

        string newReaderName = "New Reader";

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(emptyConfig))
            .Add(p => p.ExtensionId, _extensionId));

        var addReaderButton = _cut.FindComponents<Button>()
            .First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Reader");

        await _cut.InvokeAsync(async () => await addReaderButton.Instance.Clicked.InvokeAsync());

        var textEdit = _cut.Find("#NameTextEdit");
        textEdit.Input(newReaderName);

        var modalAddButton =
            _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add");
        await _cut.InvokeAsync(async () => await modalAddButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddUpdateReader.ToString(),
            JsonConvert.SerializeObject(new Device { Name = newReaderName, Number = 0 })));
    }
    
    [Test]
    public async Task EditReader()
    {
        // Arrange
        var readersOnRazorPage = new List<Device>
        {
            new() { Name = "Virtual Reader 1", Number = 1 },
            new() { Name = "Virtual Reader 2", Number = 2 },
            new() { Name = "Virtual Reader 3", Number = 3 }
        };

        Endpoint[] availableEndPoints =
        {
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Reader,
                DriverEndpointId = $"VR{readersOnRazorPage[1].Number}", Id = readersOnRazorPage[1].Number
            },
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Reader,
                DriverEndpointId = $"VR{readersOnRazorPage[2].Number}", Id = readersOnRazorPage[2].Number
            }
        };

        SetUpDoorMock(availableEndPoints);

        var config = new Shared.Configuration();

        config.Readers.AddRange(readersOnRazorPage);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId));

        var editButton = _cut.FindComponents<DropdownItem>().First(button =>
            !button.Instance.Disabled && button.Nodes[0].TextContent.Trim() == "Edit");

        await _cut.InvokeAsync(async () => await editButton.Instance.Clicked.InvokeAsync());
        
        var textEdit = _cut.Find("#NameTextEdit");
        textEdit.Input("Edit Reader Name");

        var modalEditButton =
            _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Edit");
        await _cut.InvokeAsync(async () => await modalEditButton.Instance.Clicked.InvokeAsync());
        
        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddUpdateReader.ToString(),
            JsonConvert.SerializeObject(new Device { Name = "Edit Reader Name", Number = 1 })));
    }

    [Test]
    public async Task RemoveReader()
    {
        // Arrange
        var readersOnRazorPage = new List<Device>
        {
            new() { Name = "Virtual Reader 1", Number = 1 },
            new() { Name = "Virtual Reader 2", Number = 2 },
            new() { Name = "Virtual Reader 3", Number = 3 }
        };

        Endpoint[] availableEndPoints =
        {
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Reader,
                DriverEndpointId = $"VR{readersOnRazorPage[1].Number}", Id = readersOnRazorPage[1].Number
            },
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Reader,
                DriverEndpointId = $"VR{readersOnRazorPage[2].Number}", Id = readersOnRazorPage[2].Number
            }
        };

        SetUpDoorMock(availableEndPoints);

        var config = new Shared.Configuration();

        config.Readers.AddRange(readersOnRazorPage);

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

        var deleteButton = _cut.FindComponents<DropdownItem>().First(button =>
            !button.Instance.Disabled && button.Nodes[0].TextContent.Trim() == "Delete");

        await _cut.InvokeAsync(async () => await deleteButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.RemoveReader.ToString(),
            JsonConvert.SerializeObject(readerToRemove)));
    }

    [Test]
    public async Task AddInput()
    {
        // Arrange
        SetUpDoorMock();

        var emptyConfig = new Shared.Configuration();

        string newInputName = "New Input";

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(emptyConfig))
            .Add(p => p.ExtensionId, _extensionId));

        var addInputButton = _cut.FindComponents<Button>()
            .First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Input");

        await _cut.InvokeAsync(async () => await addInputButton.Instance.Clicked.InvokeAsync());

        var textEdit = _cut.Find("#NameTextEdit");
        textEdit.Input(newInputName);

        var modalAddButton =
            _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add");
        await _cut.InvokeAsync(async () => await modalAddButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddUpdateInput.ToString(),
            JsonConvert.SerializeObject(new Device { Name = newInputName, Number = 0 })));
    }
    
     [Test]
    public async Task EditInput()
    {
        // Arrange
        var inputsOnRazorPage = new List<Device>
        {
            new() { Name = "Virtual Input 1", Number = 1 },
            new() { Name = "Virtual Input 2", Number = 2 },
            new() { Name = "Virtual Input 3", Number = 3 }
        };

        Endpoint[] availableEndPoints =
        {
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Input,
                DriverEndpointId = $"VI{inputsOnRazorPage[1].Number}", Id = inputsOnRazorPage[1].Number
            },
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Input,
                DriverEndpointId = $"VI{inputsOnRazorPage[2].Number}", Id = inputsOnRazorPage[2].Number
            }
        };

        SetUpDoorMock(availableEndPoints);

        var config = new Shared.Configuration();

        config.Inputs.AddRange(inputsOnRazorPage);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId));

        var editButton = _cut.FindComponents<DropdownItem>().First(button =>
            !button.Instance.Disabled && button.Nodes[0].TextContent.Trim() == "Edit");

        await _cut.InvokeAsync(async () => await editButton.Instance.Clicked.InvokeAsync());
        
        var textEdit = _cut.Find("#NameTextEdit");
        textEdit.Input("Edit Input Name");

        var modalEditButton =
            _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Edit");
        await _cut.InvokeAsync(async () => await modalEditButton.Instance.Clicked.InvokeAsync());
        
        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddUpdateInput.ToString(),
            JsonConvert.SerializeObject(new Device { Name = "Edit Input Name", Number = 1 })));
    }

    [Test]
    public async Task RemoveInput()
    {
        // Arrange
        var inputsOnRazorPage = new List<Device>
        {
            new() { Name = "Virtual Input 1", Number = 1 },
            new() { Name = "Virtual Input 2", Number = 2 },
            new() { Name = "Virtual Input 3", Number = 3 }
        };

        //set up endpoints not assigned to a door
        Endpoint[] availableEndPoints =
        {
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Input,
                DriverEndpointId = $"VI{inputsOnRazorPage[1].Number}", Id = inputsOnRazorPage[1].Number
            },
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Input,
                DriverEndpointId = $"VI{inputsOnRazorPage[2].Number}", Id = inputsOnRazorPage[2].Number
            }

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

        var deleteButton = _cut.FindComponents<DropdownItem>().First(button =>
            !button.Instance.Disabled && button.Nodes[0].TextContent.Trim() == "Delete");

        await _cut.InvokeAsync(async () => await deleteButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.RemoveInput.ToString(),
            JsonConvert.SerializeObject(inputToRemove)));
    }

    [Test]
    public async Task AddOutput()
    {
        // Arrange
        SetUpDoorMock();

        var emptyConfig = new Shared.Configuration();

        string newOutputName = "New Output";

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(emptyConfig))
            .Add(p => p.ExtensionId, _extensionId));

        var addOutputButton = _cut.FindComponents<Button>()
            .First(button => button.Nodes[0].TextContent.Trim() == "Add Virtual Output");

        await _cut.InvokeAsync(async () => await addOutputButton.Instance.Clicked.InvokeAsync());

        var textEdit = _cut.Find("#NameTextEdit");
        textEdit.Input(newOutputName);

        var modalAddOutputButton =
            _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Add");

        await _cut.InvokeAsync(async () => await modalAddOutputButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddUpdateOutput.ToString(),
            JsonConvert.SerializeObject(new Device { Name = newOutputName, Number = 0 })));
    }

         [Test]
    public async Task EditOutput()
    {
        // Arrange
        var outputsOnRazorPage = new List<Device>
        {
            new() { Name = "Virtual Output 1", Number = 1 },
            new() { Name = "Virtual Output 2", Number = 2 },
            new() { Name = "Virtual Output 3", Number = 3 }
        };

        Endpoint[] availableEndPoints =
        {
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Output,
                DriverEndpointId = $"VO{outputsOnRazorPage[1].Number}", Id = outputsOnRazorPage[1].Number
            },
            new()
            {
                ExtensionId = _extensionId, Type = EndpointType.Output,
                DriverEndpointId = $"VO{outputsOnRazorPage[2].Number}", Id = outputsOnRazorPage[2].Number
            }
        };

        SetUpDoorMock(availableEndPoints);

        var config = new Shared.Configuration();

        config.Outputs.AddRange(outputsOnRazorPage);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(config))
            .Add(p => p.ExtensionId, _extensionId));

        var editButton = _cut.FindComponents<DropdownItem>().First(button =>
            !button.Instance.Disabled && button.Nodes[0].TextContent.Trim() == "Edit");

        await _cut.InvokeAsync(async () => await editButton.Instance.Clicked.InvokeAsync());
        
        var textEdit = _cut.Find("#NameTextEdit");
        textEdit.Input("Edit Output Name");

        var modalEditButton =
            _cut.FindComponents<Button>().First(button => button.Nodes[0].TextContent.Trim() == "Edit");
        await _cut.InvokeAsync(async () => await modalEditButton.Instance.Clicked.InvokeAsync());
        
        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddUpdateOutput.ToString(),
            JsonConvert.SerializeObject(new Device { Name = "Edit Output Name", Number = 1 })));
    }
    
    [Test]
    public async Task RemoveOutput()
    {
        // Arrange
        var outputsOnRazorPage = new List<Device>
        {
            new() {Name = "Virtual Output 1",Number = 1 },
            new() {Name = "Virtual Output 2",Number = 2 },
            new() {Name = "Virtual Output 3",Number = 3 }
        };

        //set up endpoints not assigned to a door
        Endpoint[] availableEndPoints = {
            new() { ExtensionId = _extensionId, Type = EndpointType.Output, DriverEndpointId = $"VO{outputsOnRazorPage[1].Number}", Id = outputsOnRazorPage[1].Number },
            new() { ExtensionId = _extensionId, Type = EndpointType.Output, DriverEndpointId = $"VO{outputsOnRazorPage[2].Number}", Id = outputsOnRazorPage[2].Number }

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

        var deleteButton = _cut.FindComponents<DropdownItem>().First(button => !button.Instance.Disabled && button.Nodes[0].TextContent.Trim() == "Delete");

        await _cut.InvokeAsync(async () => await deleteButton.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.RemoveOutput.ToString(), JsonConvert.SerializeObject(outputToRemove)));
    }
}