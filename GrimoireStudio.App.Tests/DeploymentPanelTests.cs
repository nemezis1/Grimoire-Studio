using Bunit;
using Grimoire_Studio.Components;

namespace GrimoireStudio.App.Tests;

public class DeploymentPanelTests : AppTestContext
{
    [Fact]
    public void Refresh_Should_BeHidden_When_NothingDeploys()
    {
        var panel = Render<DeploymentPanel>();

        Assert.DoesNotContain(panel.FindAll("button"), b => b.TextContent.Contains("Odśwież"));
    }

    [Fact]
    public void Refresh_Should_ShowAtOnce_When_CommitSent()
    {
        var panel = Render<DeploymentPanel>();

        panel.Render(p => p.Add(x => x.Sent, "abc1234"));

        Assert.Contains(panel.FindAll("button"), b => b.TextContent.Contains("Odśwież"));
    }
}
