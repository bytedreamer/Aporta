using Aporta.Shared.Calls;
using Aporta.Shared.Models;

using Microsoft.Extensions.DependencyInjection;

namespace Aporta.WebClient.Tests.Pages.configuration;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DriversTests : AportaTestContext
{
    private readonly Mock<IExtensionCalls> _mockExtensionCalls = new();
    private readonly Extension[] _extensions = new[]
    {
        new Extension { Enabled = false, Id = Guid.NewGuid(), Name = "Test Disabled Driver" },
        new Extension { Enabled = true, Id = Guid.NewGuid(), Name = "Test Enabled Driver" }
    };
    
    public DriversTests()
    {
        Services.AddScoped<IExtensionCalls>(_ => _mockExtensionCalls.Object);
    }
    
    [Test]
    public async Task DriversComponentRendersCorrectly()
    {
        // Act
        var cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        // Assert
        string headerText = cut.Find("h1").InnerHtml;
        Assert.That(headerText, Is.EqualTo("Drivers"));
        
        await cut.Instance.DisposeAsync();
    }

    [Test]
    public async Task DriversComponentShowAllDrivers()
    {
        // Arrange
        _mockExtensionCalls.Setup(calls => calls.GetAll()).ReturnsAsync(_extensions);

        // Act
        var cut = RenderComponent<WebClient.Pages.configuration.Drivers>();
        
        var rowHeaders = cut.FindAll("th > a");
        Assert.That(rowHeaders, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(rowHeaders.Any(rowHeader => rowHeader.InnerHtml == "Test Disabled Driver"));
            Assert.That(rowHeaders.Any(rowHeader => rowHeader.InnerHtml == "Test Enabled Driver"));
        });
        
        await cut.Instance.DisposeAsync();
    }
    
    [Test]
    public async Task DriversComponentHandleDriverLoadingProblem()
    {
        // Arrange
        const string errorMessage = "Loading issue";
        _mockExtensionCalls.Setup(calls => calls.GetAll()).Throws(new Exception(errorMessage));

        // Act
        var cut = RenderComponent<WebClient.Pages.configuration.Drivers>();
        cut.WaitForElement(".snackbar-body");

        var snackbarBody = cut.Find(".snackbar-body");
        Assert.That(snackbarBody.InnerHtml, Contains.Substring(errorMessage));

        var rowHeaders = cut.FindAll("th > a");
        Assert.That(rowHeaders, Has.Count.EqualTo(0));

        await cut.Instance.DisposeAsync();
    }
}