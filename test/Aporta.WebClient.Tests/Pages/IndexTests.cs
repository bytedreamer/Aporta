namespace Aporta.WebClient.Tests.Pages;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class IndexTests : AportaTestContext
{
    [Test]
    public void IndexComponentRendersCorrectly()
    {
        // Act
        using var cut = RenderComponent<WebClient.Pages.Index>();
        string headerText = cut.Find("h1").InnerHtml;

        // Assert
        Assert.That(headerText, Is.EqualTo("Home"));
    }
}