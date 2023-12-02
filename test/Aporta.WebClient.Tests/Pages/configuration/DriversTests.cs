using Aporta.Shared.Calls;
using Aporta.Shared.Messaging;
using Aporta.Shared.Models;

using Blazorise;
using Blazorise.Snackbar;
using Microsoft.Extensions.DependencyInjection;

namespace Aporta.WebClient.Tests.Pages.configuration;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DriversTests : AportaTestContext
{
    private readonly Mock<IExtensionCalls> _mockExtensionCalls = new();
    private readonly Extension[] _extensions = {
        new() { Enabled = false, Loaded = false, Id = Guid.NewGuid(), Name = "Test Disabled Driver" },
        new() { Enabled = true, Loaded = true, Id = Guid.NewGuid(), Name = "Test Enabled Driver" }
    };

    private IRenderedComponent<WebClient.Pages.configuration.Drivers>? _cut;
    
    public DriversTests()
    {
        Services.AddScoped<IExtensionCalls>(_ => _mockExtensionCalls.Object);
    }
    
    [Test]
    public void DriversComponentRendersCorrectly()
    {
        // Act
        _cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        // Assert
        Assert.That(_cut.FindComponent<Heading>().Nodes[0].TextContent, Is.EqualTo("Drivers"));
    }

    [Test]
    public void DriversComponentShowAllDrivers()
    {
        // Arrange
        _mockExtensionCalls.Setup(calls => calls.GetAll()).ReturnsAsync(_extensions);

        // Act
        _cut = RenderComponent<WebClient.Pages.configuration.Drivers>();
        
        var rowHeaders = _cut.FindComponents<TableRowHeader>();
        Assert.That(rowHeaders, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(rowHeaders.Any(rowHeader => rowHeader.Nodes[0].TextContent == _extensions[0].Name));
            Assert.That(rowHeaders.Any(rowHeader => rowHeader.Nodes[0].TextContent == _extensions[1].Name));
        });
    }
    
    [Test]
    public void DriversComponentHandleDriverLoadingProblem()
    {
        // Arrange
        const string errorMessage = "Loading issue";
        _mockExtensionCalls.Setup(calls => calls.GetAll()).Throws(new Exception(errorMessage));

        // Act
        _cut = RenderComponent<WebClient.Pages.configuration.Drivers>();
        _cut.WaitForElement(".snackbar-body");

        // Assert
        var snackbarBody = _cut.FindComponent<SnackbarBody>();
        Assert.That(snackbarBody.Nodes[0].TextContent, Contains.Substring(errorMessage));

        var rowHeaders = _cut.FindComponents<TableRowHeader>();
        Assert.That(rowHeaders, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task DriversComponentOnDriverConfigurationUpdate()
    {
        // Arrange
        _mockExtensionCalls.Setup(calls => calls.GetAll()).ReturnsAsync(_extensions);

        Func<Guid, Task>? onMethodCallback = null;
        HubProxyMock.Setup(connection =>
                connection.On(Methods.ExtensionDataChanged, It.IsAny<Func<Guid, Task>>()))
            .Callback<string, Func<Guid, Task>>((_, callback) => { onMethodCallback = callback; });

        // Act
        _cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        _extensions[0].Name += " Updated";

        if (onMethodCallback != null)
            await _cut.InvokeAsync(async () => await onMethodCallback.Invoke(_extensions[0].Id));

        // Assert
        var rowHeaders = _cut.FindComponents<TableRowHeader>();
        Assert.That(rowHeaders, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(rowHeaders.Any(rowHeader => rowHeader.Nodes[0].TextContent == _extensions[0].Name));
            Assert.That(rowHeaders.Any(rowHeader => rowHeader.Nodes[0].TextContent == _extensions[1].Name));
        });
    }

    [TestCase(true, true, ExpectedResult = IconName.Check)]
    [TestCase(true, false, ExpectedResult = IconName.ExclamationTriangle)]
    [TestCase(false, false, ExpectedResult = IconName.MinusCircle)]
    public IconName DriversComponentShowTheCorrectStatusIcon(bool enabled, bool loaded)
    {
        // Arrange
        _extensions[0].Enabled = enabled;
        _extensions[0].Loaded = loaded;
        _mockExtensionCalls.Setup(calls => calls.GetAll()).ReturnsAsync(_extensions);

        // Act
        _cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        var tableRows = _cut.FindComponent<TableBody>().FindComponents<TableRow>();
        var statusIcons = tableRows.Select(row => row.FindComponents<TableRowCell>()[0].FindComponent<Icon>());

        // Assert
        return (IconName)statusIcons.First().Instance.Name;
    }

    [TestCase(true, true, ExpectedResult = "color: green")]
    [TestCase(true, false, ExpectedResult = "color: orange")]
    [TestCase(false, false, ExpectedResult = "color: red")]
    public string DriversComponentShowTheCorrectStatusColor(bool enabled, bool loaded)
    {
        // Arrange
        _extensions[0].Enabled = enabled;
        _extensions[0].Loaded = loaded;
        _mockExtensionCalls.Setup(calls => calls.GetAll()).ReturnsAsync(_extensions);

        // Act
        _cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        var tableRows = _cut.FindComponent<TableBody>().FindComponents<TableRow>();
        var statusIcons = tableRows.Select(row => row.FindComponents<TableRowCell>()[0].FindComponent<Icon>());

        // Assert
        return statusIcons.First().Instance.Style;
    }

    [Test]
    public async Task DriversComponentEnableDriver()
    {
        // Arrange
        _mockExtensionCalls.Setup(calls => calls.GetAll()).ReturnsAsync(_extensions);
        _mockExtensionCalls.Setup(calls => calls.ChangeEnableSettings(_extensions[0].Id, true)).Verifiable();

        // Act
        _cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        var disabledDriverRow = _cut.FindComponent<TableBody>().FindComponents<TableRow>()[0];
        var enableMenuItem = disabledDriverRow.FindComponents<DropdownItem>()
            .First(item => item.Nodes[0].TextContent == "Enable");
        await _cut.InvokeAsync(async () => await enableMenuItem.Instance.Clicked.InvokeAsync());

        // Assert
        _mockExtensionCalls.Verify();
    }
    
    [Test]
    public async Task DriversComponentDisableDriver()
    {
        // Arrange
        _mockExtensionCalls.Setup(calls => calls.GetAll()).ReturnsAsync(_extensions);
        _mockExtensionCalls.Setup(calls => calls.ChangeEnableSettings(_extensions[1].Id, false)).Verifiable();

        // Act
        _cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        var disabledDriverRow = _cut.FindComponent<TableBody>().FindComponents<TableRow>()[1];
        var enableMenuItem = disabledDriverRow.FindComponents<DropdownItem>()
            .First(item => item.Nodes[0].TextContent == "Disable");
        await _cut.InvokeAsync(async () => await enableMenuItem.Instance.Clicked.InvokeAsync());

        // Assert
        _mockExtensionCalls.Verify();
    }

    [TearDown]
    public async Task Cleanup()
    {
        if (_cut != null) await _cut.Instance.DisposeAsync();
    }
}