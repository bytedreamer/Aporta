using RichardSzalay.MockHttp;

using Aporta.Shared.Calls;
using Aporta.Shared.Models;

namespace Aporta.WebClient.Tests.Pages.configuration;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DriversTests : AportaTestContext
{
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
        var request = Mock.When(MockHttpClientBunitHelpers.BuildUrl(Paths.Extensions)).RespondJson(new List<Extension>
            { new Extension { Enabled = false, Id = Guid.NewGuid(), Name = "Test Driver" } });

        // Act
        var cut = RenderComponent<WebClient.Pages.configuration.Drivers>();
        cut.WaitForState(() => Mock.GetMatchCount(request) > 0);
        Mock.Flush();
        cut.WaitForState(() => cut.FindAll("th > a").Count == 1);
        
        // Assert
        var rowHeaders = cut.FindAll("th > a");
        Assert.That(rowHeaders.Any(rowHeader => rowHeader.InnerHtml == "Test Driver"));
    }
}