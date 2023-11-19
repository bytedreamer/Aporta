using Aporta.Shared.Calls;
using Aporta.Shared.Models;

using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace Aporta.WebClient.Tests.Pages.configuration;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DriversTests : AportaTestContext
{
    private readonly Mock<IExtensionCalls> _mockExtensionCalls = new();
    
    public DriversTests()
    {
        Services.AddScoped<IExtensionCalls>(_ => _mockExtensionCalls.Object);
    }
    
    [Test]
    public void DriversComponentRendersCorrectly()
    {
        // Act
        using var cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        // Assert
        string headerText = cut.Find("h1").InnerHtml;
        Assert.That(headerText, Is.EqualTo("Drivers"));
    }

    [Test]
    public void DriversComponentShowAllDrivers()
    {
        // Arrange
        _mockExtensionCalls.Setup(calls => calls.GetAll()).ReturnsAsync(new[]
            { new Extension { Enabled = false, Id = Guid.NewGuid(), Name = "Test Driver" } });

        // Act
        var cut = RenderComponent<WebClient.Pages.configuration.Drivers>();

        // Assert
        var rowHeaders = cut.FindAll("th > a");
        Assert.That(rowHeaders.Any(rowHeader => rowHeader.InnerHtml == "Test Driver"));
    }
}