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

    private readonly Guid _extensionId = Guid.NewGuid();
    
    private IRenderedComponent<Configuration>? _cut;
    
    public ConfigurationTest()
    {
        BlazoriseConfig.JSInterop.AddTextEdit(JSInterop);
        BlazoriseConfig.JSInterop.AddButton(JSInterop);
        BlazoriseConfig.JSInterop.AddTooltip(JSInterop);
        
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

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, TestConfiguration)
            .Add(p => p.ExtensionId, _extensionId));

        var addBusButton = _cut.FindComponents<Button>().Single(button => button.Nodes[0].TextContent.Trim() == "Add RS-485 Port");
        await _cut.InvokeAsync(async () => await addBusButton.Instance.Clicked.InvokeAsync());

        var modal = _cut.FindComponents<Modal>().First(modal => modal.Instance.ClassNames.Contains("show"));
        var modalAddButton = modal.FindComponents<Button>().Single(button => button.Nodes[0].TextContent.Trim() == "Add");
        await _cut.InvokeAsync(async () => await modalAddButton.Instance.Clicked.InvokeAsync());
        
        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.AddSerialBus.ToString(), busActionParameters));
    }

    [Test]
    public async Task ConfirmDeleteBus()
    {
        // Arrange
        string busActionParameters = JsonConvert.SerializeObject(new BusAction
        {
            Bus = new Bus { BaudRate = 9600, PortName = "COM3" }
        });

        // Act
        var confirmMessage = new Mock<IMessageService>();
        confirmMessage
            .Setup(confirm => confirm.Confirm(It.IsAny<string>(), "Delete Bus", It.IsAny<Action<MessageOptions>>()))
            .ReturnsAsync(true);
        Services.AddSingleton(confirmMessage.Object);

        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, TestConfiguration)
            .Add(p => p.ExtensionId, _extensionId));
        
        var busRow = _cut.FindComponents<Row>().Single(row => row.Instance.ElementId == $"Bus:COM3");
        var deleteBusMenuItem = busRow.FindComponents<DropdownItem>()
            .First(item => item.Nodes[0].TextContent == "Delete");
        await _cut.InvokeAsync(async () => await deleteBusMenuItem.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.RemoveSerialBus.ToString(), busActionParameters), Times.Once);
    }
    
    [Test]
    public async Task NotConfirmDeleteBus()
    {
        // Arrange
        string busActionParameters = JsonConvert.SerializeObject(new BusAction
        {
            Bus = new Bus { BaudRate = 9600, PortName = "COM3" }
        });

        // Act
        var confirmMessage = new Mock<IMessageService>();
        confirmMessage
            .Setup(confirm => confirm.Confirm(It.IsAny<string>(), "Delete Bus", It.IsAny<Action<MessageOptions>>()))
            .ReturnsAsync(false);
        Services.AddSingleton(confirmMessage.Object);

        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, TestConfiguration)
            .Add(p => p.ExtensionId, _extensionId));

        var busRow = _cut.FindComponents<Row>().Single(row => row.Instance.ElementId == $"Bus:COM3");
        var deleteBusMenuItem = busRow.FindComponents<DropdownItem>()
            .First(item => item.Nodes[0].TextContent == "Delete");
        await _cut.InvokeAsync(async () => await deleteBusMenuItem.Instance.Clicked.InvokeAsync());

        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.RemoveSerialBus.ToString(), busActionParameters), Times.Never());
    }
    
    [Test]
    public async Task EnablePkoc()
    {
        // Arrange
        var testDevice = new Device { Address = 0, PKOCEnabled = false, Name = "TestDevice", PortName = "COM3" };
        
        string deviceActionParameters = JsonConvert.SerializeObject(new DeviceAction { Device = testDevice });
        
        var testConfigurationWithDevice = AddDeviceToConfiguration(testDevice);
        
        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(testConfigurationWithDevice))
            .Add(p => p.ExtensionId, _extensionId));
        
        var deviceTableRow = _cut.FindComponents<TableRow>().Single(row => row.Instance.ElementId == $"Device:{testDevice.Name}");
        var enablePkocMenuItem = deviceTableRow.FindComponents<DropdownItem>()
            .First(item => item.Nodes[0].TextContent == "Enable PKOC");
        await _cut.InvokeAsync(async () => await enablePkocMenuItem.Instance.Clicked.InvokeAsync());
        
        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.EnablePKOC.ToString(), deviceActionParameters));
    }
    
    [Test]
    public async Task DisablePkoc()
    {
        // Arrange
        var testDevice = new Device { Address = 0, PKOCEnabled = true, Name = "TestDevice", PortName = "COM3" };
        
        string deviceActionParameters = JsonConvert.SerializeObject(new DeviceAction { Device = testDevice });
        
        var testConfigurationWithDevice = AddDeviceToConfiguration(testDevice);
        
        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(testConfigurationWithDevice))
            .Add(p => p.ExtensionId, _extensionId));
        
        var deviceTableRow = _cut.FindComponents<TableRow>().Single(row => row.Instance.ElementId == $"Device:{testDevice.Name}");
        var enablePkocMenuItem = deviceTableRow.FindComponents<DropdownItem>()
            .First(item => item.Nodes[0].TextContent == "Disable PKOC");
        await _cut.InvokeAsync(async () => await enablePkocMenuItem.Instance.Clicked.InvokeAsync());
        
        // Assert
        _mockConfigurationCalls.Verify(calls => calls.PerformAction(_extensionId, ActionType.DisablePKOC.ToString(), deviceActionParameters));
    }


    [TestCase(true, ExpectedResult = IconName.Key)]
    [TestCase(false, ExpectedResult = null)]
    public IconName? DriversComponentShowTheCorrectStatusIcon(bool pkocEnabled)
    {
        // Arrange
        var testDevice = new Device { Address = 0, PKOCEnabled = pkocEnabled, Name = "TestDevice", PortName = "COM3" };

        var testConfigurationWithDevice = AddDeviceToConfiguration(testDevice);

        // Act
        _cut = RenderComponent<Configuration>(parameters => parameters
            .Add(p => p.RawConfiguration, JsonConvert.SerializeObject(testConfigurationWithDevice))
            .Add(p => p.ExtensionId, _extensionId));

        var deviceTableRow = _cut.FindComponents<TableRow>()
            .Single(row => row.Instance.ElementId == $"Device:{testDevice.Name}");
        var pkocStatusIcon = deviceTableRow.FindComponents<TableRowCell>()[0].FindComponents<Icon>()
            .FirstOrDefault(icon => icon.Instance.ElementId == "PKOCStatusIcon");

        // Assert
        return (IconName?)pkocStatusIcon?.Instance.Name;
    }

    private static Shared.Configuration? AddDeviceToConfiguration(Device testDevice)
    {
        var testConfigurationWithDevice = JsonConvert.DeserializeObject<Shared.Configuration>(TestConfiguration);
        if (testConfigurationWithDevice == null)
        {
            Assert.Fail("Unable to deserialize test configuration data");
            return null;
        }

        testConfigurationWithDevice.Buses.First().Devices.Add(testDevice);
        return testConfigurationWithDevice;
    }
}