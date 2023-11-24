using Blazorise;

namespace Aporta.WebClient.Tests.Pages;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class IndexTests : AportaTestContext
{
    [Test]
    public void IndexComponentRendersCorrectly()
    {
        // Act
        using var cut = RenderComponent<WebClient.Pages.Index>();
       
        // Assert
        Assert.That(cut.FindComponent<Heading>().Nodes[0].TextContent, Is.EqualTo("Home"));
    }
}