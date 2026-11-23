using Bunit;
using Grimoire_Studio.Pages;
using Grimoire_Studio.Services;
using Microsoft.AspNetCore.Components;

namespace GrimoireStudio.App.Tests;

public class RolePreferenceTests : AppTestContext
{
    public RolePreferenceTests()
    {
        LoadData();
    }

    private IRenderedComponent<Roles> RenderRoles(string address = "roles")
    {
        Service<NavigationManager>().NavigateTo(address);
        var page = Render<Roles>();
        page.WaitForAssertion(() => Assert.Contains("Znaleziono: ", page.Markup));
        return page;
    }

    private static int Found(IRenderedComponent<Roles> page) =>
        int.Parse(page.Find(".filters__count").TextContent.Split(':')[1].Trim().Split(' ', '\n')[0]);

    private static AngleSharp.Dom.IElement Card(IRenderedComponent<Roles> page, string name) =>
        page.FindAll(".role-grid__item").First(li => li.QuerySelector(".role-card__name")!.TextContent.Trim().StartsWith(name, StringComparison.Ordinal));

    [Fact]
    public void Dislike_Should_HideTheRoleFromTheList_When_Clicked()
    {
        var page = RenderRoles();
        var all = Found(page);

        Card(page, "Kucharz").QuerySelector(".role-preference__button--dislike")!.Click();

        page.WaitForAssertion(() => Assert.Equal(all - 1, Found(page)));
        Assert.DoesNotContain(page.FindAll(".role-card__name"), n => n.TextContent.Trim().StartsWith("Kucharz", StringComparison.Ordinal));
        Assert.Contains("\"chef\":\"dislike\"", Browser.Local["grimoire-studio.role-preferences"]);
    }

    [Fact]
    public async Task Filter_Should_ListOnlyLikedOrShowDislikedFaded_When_Chosen()
    {
        var preferences = Service<RolePreferences>();
        await preferences.ToggleAsync("chef", RolePreferences.Like);
        await preferences.ToggleAsync("imp", RolePreferences.Dislike);

        var liked = RenderRoles("roles?show=liked");
        Assert.Equal(1, Found(liked));
        Assert.Contains("Kucharz", liked.Find(".role-card__name").TextContent);

        var disliked = RenderRoles("roles?show=disliked");
        Assert.Equal(1, Found(disliked));
        Assert.Contains("Chochlik", disliked.Find(".role-card__name").TextContent);

        // All three chips: the disliked roles come back, faded.
        var all = RenderRoles("roles?show=liked,neutral,disliked");
        Assert.Contains("role-grid__item--disliked", Card(all, "Chochlik").ClassName);
        Assert.DoesNotContain("role-grid__item--disliked", Card(all, "Kucharz").ClassName);
    }

    [Fact]
    public void Select_Should_TickLikedAndUnmarkedByDefault_And_ToggleKinds()
    {
        var navigation = Service<NavigationManager>();
        var page = RenderRoles();
        AngleSharp.Dom.IElement Box(string label) =>
            page.FindAll(".preference-select label").First(l => l.TextContent.Contains(label, StringComparison.Ordinal)).QuerySelector("input")!;

        Assert.Equal("Ulubione i pozostałe", page.Find(".preference-select summary").TextContent.Trim());
        Assert.True(Box("Ulubione").HasAttribute("checked"));
        Assert.False(Box("Nielubiane").HasAttribute("checked"));

        Box("Nielubiane").Change(true);
        page.WaitForAssertion(() => Assert.Contains("show=liked,neutral,disliked", Uri.UnescapeDataString(navigation.Uri)));

        page = RenderRoles("roles?show=disliked");
        Assert.Equal("Nielubiane", page.Find(".preference-select summary").TextContent.Trim());

        // Unticking the last kind lists no role.
        page = RenderRoles("roles?show=disliked");
        Box("Nielubiane").Change(false);
        page.WaitForAssertion(() => Assert.EndsWith("show=none", navigation.Uri));
        page = RenderRoles("roles?show=none");
        Assert.Equal(0, Found(page));
        Assert.Equal("Nic nie zaznaczono", page.Find(".preference-select summary").TextContent.Trim());
    }

    [Fact]
    public async Task Toggle_Should_Unmark_When_ClickedAgain()
    {
        var preferences = Service<RolePreferences>();
        await preferences.ToggleAsync("chef", RolePreferences.Like);
        await preferences.ToggleAsync("chef", RolePreferences.Like);

        Assert.Null(preferences.Get("chef"));
    }
}
