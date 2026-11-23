using Bunit;
using Grimoire_Studio.Pages;
using Grimoire_Studio.Services;
using GrimoireStudio.Data;
using Microsoft.AspNetCore.Components;

namespace GrimoireStudio.App.Tests;

/// <summary>"Zgłoś nową rolę" without the publishing service: the role and the proposal stay in the (fake) browser.</summary>
public class ProposeRoleTests : AppTestContext
{
    public ProposeRoleTests()
    {
        LoadData();
    }

    private IRenderedComponent<RoleEditor> RenderEditor(string address)
    {
        Service<NavigationManager>().NavigateTo(address);
        var page = Render<RoleEditor>();
        page.WaitForAssertion(() => Assert.DoesNotContain("Wczytywanie", page.Markup));
        return page;
    }

    private static AngleSharp.Dom.IElement Field(IRenderedComponent<RoleEditor> page, string label) =>
        page.FindAll("label.field").First(l => l.QuerySelector("span")!.TextContent.Trim() == label).QuerySelector("input, textarea")!;

    private static AngleSharp.Dom.IElement SendButton(IRenderedComponent<RoleEditor> page) =>
        page.FindAll("button").Single(b => b.TextContent.Contains("Wyślij do akceptacji", StringComparison.Ordinal));

    [Fact]
    public async Task ProposeRole_Should_SaveTheRoleLocallyAndSendIt_When_Sent()
    {
        var page = RenderEditor("roles/propose");
        Assert.Contains("Zgłoś nową rolę", page.Find("h1").TextContent);

        Field(page, "Nazwa (PL)").Input("Latarnik");
        Field(page, "Po polsku").Input("Każdej nocy wybierz gracza: dowiadujesz się, czy jest Demonem.");
        Assert.True(SendButton(page).HasAttribute("disabled"));

        Field(page, "Podpis").Input("Tester");
        Field(page, "Komentarz dla administratora").Input("Rola z naszej grupy.");
        SendButton(page).Click();

        page.WaitForAssertion(() => Assert.EndsWith("/changes", Service<NavigationManager>().Uri));
        var local = await Service<ChangeSets>().Personal.GetAllAsync();
        Assert.Equal([LocalBackup.RoleKey("latarnik")], local.Keys());

        var proposal = Assert.Single(await Service<Proposals>().GetAllAsync());
        Assert.Equal("Tester", proposal.Author);
        Assert.Equal("Rola z naszej grupy.", proposal.Comment);
        Assert.Equal([LocalBackup.RoleKey("latarnik")], proposal.Changes.Keys());
        Assert.Equal("Tester", Browser.Local["grimoire-studio.author"]);
    }

    [Fact]
    public void EnglishToggle_Should_FollowTheRolesTranslation_When_Opened()
    {
        Service<NavigationManager>().NavigateTo("roles/imp/edit");
        var translated = Render<RoleEditor>(p => p.Add(e => e.Id, "imp"));
        translated.WaitForAssertion(() => Assert.Contains("Chochlik", translated.Find(".role-editor input[type=text]").GetAttribute("value")));
        Assert.True(translated.Find(".role-editor__english input").HasAttribute("checked"));
        Assert.Contains("Nazwa (EN)", translated.Markup);

        var fresh = RenderEditor("roles/new");
        Assert.False(fresh.Find(".role-editor__english input").HasAttribute("checked"));
        Assert.DoesNotContain("Nazwa (EN)", fresh.Markup);
    }

    [Fact]
    public async Task Editor_Should_LeaveEnglishEmpty_When_RoleWasSavedOnlyInPolish()
    {
        // Saving without a translation copies the Polish texts into the English ones.
        var role = new Role { Id = "latarnik", NamePl = "Latarnik", NameEn = "Latarnik", AbilityPl = "Zdolność.", AbilityEn = "Zdolność." };
        await Service<ChangeSets>().Personal.Roles.SaveAsync(new LocalRole(role, null));

        Service<NavigationManager>().NavigateTo("roles/latarnik/edit");
        var page = Render<RoleEditor>(p => p.Add(e => e.Id, "latarnik"));
        page.WaitForAssertion(() => Assert.Equal("Latarnik", Field(page, "Nazwa (PL)").GetAttribute("value")));
        Assert.False(page.Find(".role-editor__english input").HasAttribute("checked"));

        page.Find(".role-editor__english input").Change(true);
        Assert.Equal(string.Empty, Field(page, "Nazwa (EN)").GetAttribute("value") ?? string.Empty);
        Assert.Equal(string.Empty, Field(page, "Po angielsku (opcjonalnie)").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void NewRole_Should_SaveWithoutProposal_When_NotProposing()
    {
        var page = RenderEditor("roles/new");

        Assert.Contains("Nowa rola", page.Find("h1").TextContent);
        Assert.DoesNotContain("Wyślij do akceptacji", page.Markup);
        Assert.DoesNotContain("Podpis", page.Markup);
    }
}
