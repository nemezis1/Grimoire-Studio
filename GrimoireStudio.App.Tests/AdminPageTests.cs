using Bunit;
using Grimoire_Studio.Pages;
using Grimoire_Studio.Services;
using GrimoireStudio.Data;
using Microsoft.AspNetCore.Components;

namespace GrimoireStudio.App.Tests;

public class AdminPageTests : AppTestContext
{
    private const string AdminSession = "grimoire-studio.admin";

    private readonly GameData _data;

    public AdminPageTests()
    {
        _data = LoadData();
    }

    private Role Changed(string id) => _data.FindRole(id)! with { AbilityPl = $"Nowa zdolność: {id}." };

    /// <summary>A pending proposal from Ala changing the chef and the empath.</summary>
    private async Task<Proposal> SubmitProposalAsync()
    {
        var proposal = new Proposal
        {
            Id = "p1",
            Author = "Ala",
            Comment = "Poprawki zdolności",
            CreatedAt = DateTimeOffset.Now,
            Changes = new LocalBackup { Roles = [new LocalRole(Changed("chef"), null), new LocalRole(Changed("empath"), null)] },
        };
        return await Service<Proposals>().SubmitAsync(proposal);
    }

    private IRenderedComponent<AdminPage> RenderAsAdmin()
    {
        Browser.Session[AdminSession] = "1";
        var page = Render<AdminPage>();
        page.WaitForElement(".admin-session");
        return page;
    }

    private static AngleSharp.Dom.IElement ProposalItem(IRenderedComponent<AdminPage> page, string text) =>
        page.FindAll(".admin-section .panel .change-list > li").Single(li => li.TextContent.Contains(text, StringComparison.Ordinal));

    private string Name(string id) => _data.FindRole(id)!.NamePl;

    [Fact]
    public void Render_Should_ShowNothingAndGoHome_When_NotAdmin()
    {
        var nav = Service<NavigationManager>();
        nav.NavigateTo("admin");

        var page = Render<AdminPage>();

        page.WaitForAssertion(() => Assert.Equal("http://localhost/", nav.Uri));
        Assert.Empty(page.Markup.Trim());
    }

    [Fact]
    public async Task Render_Should_CountWaitingChanges_When_ProposalPending()
    {
        await SubmitProposalAsync();

        var page = RenderAsAdmin();

        page.WaitForAssertion(() => Assert.Contains("Czekają na akceptację (2 zmiany)", page.Find(".admin-part").TextContent));
        Assert.Contains("zgłoszenia: 1", page.Find(".admin-part").TextContent);
        Assert.Contains("Poprawki zdolności", page.Markup);
        Assert.Contains("Zatwierdzone, do wysłania (0)", page.Markup);
    }

    [Fact]
    public async Task ItemApprove_Should_MoveOnlyThatItemToWiki_When_Clicked()
    {
        await SubmitProposalAsync();
        var page = RenderAsAdmin();
        page.WaitForElements(".change-list > li");

        ProposalItem(page, Name("chef")).QuerySelectorAll("button").Single(b => b.TextContent == "Zatwierdź").Click();

        page.WaitForAssertion(() => Assert.Contains("Zatwierdzono: 1 zmiana (Ala). Czeka jeszcze: 1.", page.Find(".notice").TextContent));
        var wiki = await Service<ChangeSets>().Wiki.GetAllAsync();
        Assert.Equal([LocalBackup.RoleKey("chef")], wiki.Keys());

        var proposal = (await Service<Proposals>().GetAllAsync()).Single();
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Equal([LocalBackup.RoleKey("empath")], proposal.UndecidedKeys());
        Assert.Contains("Czekają na akceptację (1 zmiana)", page.Find(".admin-part").TextContent);
        Assert.Contains("Zatwierdzone, do wysłania (1)", page.Markup);
    }

    [Fact]
    public async Task ItemReject_Should_LeaveWikiUnchanged_When_Clicked()
    {
        await SubmitProposalAsync();
        var page = RenderAsAdmin();
        page.WaitForElements(".change-list > li");

        ProposalItem(page, Name("empath")).QuerySelectorAll("button").Single(b => b.TextContent == "Odrzuć").Click();

        page.WaitForAssertion(() => Assert.Contains("Odrzucono: 1 zmiana (Ala)", page.Find(".notice").TextContent));
        Assert.Empty((await Service<ChangeSets>().Wiki.GetAllAsync()).Keys());
        var proposal = (await Service<Proposals>().GetAllAsync()).Single();
        Assert.Equal([LocalBackup.RoleKey("empath")], proposal.RejectedKeys);
    }

    [Fact]
    public async Task ApproveSelected_Should_DecideOnlyTickedItems_When_OneUnticked()
    {
        await SubmitProposalAsync();
        var page = RenderAsAdmin();
        page.WaitForElements(".change-list > li");

        ProposalItem(page, Name("empath")).QuerySelector("input[type=checkbox]")!.Change(false);
        page.WaitForAssertion(() => page.FindAll("button").Single(b => b.TextContent.StartsWith("Zatwierdź zaznaczone (1)", StringComparison.Ordinal)));
        page.FindAll("button").Single(b => b.TextContent.StartsWith("Zatwierdź zaznaczone", StringComparison.Ordinal)).Click();

        page.WaitForAssertion(() => Assert.Contains("Zatwierdzono: 1 zmiana", page.Find(".notice").TextContent));
        Assert.Equal([LocalBackup.RoleKey("chef")], (await Service<ChangeSets>().Wiki.GetAllAsync()).Keys());
    }

    [Fact]
    public async Task RemoveFromWiki_Should_ReturnItemToWaiting_When_ItCameFromProposal()
    {
        await SubmitProposalAsync();
        var page = RenderAsAdmin();
        page.WaitForElements(".change-list > li");
        page.FindAll("button").Single(b => b.TextContent.StartsWith("Zatwierdź wszystko", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => Assert.Contains("Zatwierdzone, do wysłania (2)", page.Markup));
        Assert.Contains("zgłoszenie: Ala", page.Find(".change-origin").TextContent);

        page.FindAll(".change-list > li")
            .First(li => li.TextContent.Contains(Name("chef"), StringComparison.Ordinal))
            .QuerySelectorAll("button").Single(b => b.TextContent == "Przywróć").Click();

        page.WaitForAssertion(() => Assert.Contains("Wraca do „Czekają na akceptację”: 1 zmiana.", page.Find(".notice").TextContent));
        Assert.Equal([LocalBackup.RoleKey("empath")], (await Service<ChangeSets>().Wiki.GetAllAsync()).Keys());
        var proposal = (await Service<Proposals>().GetAllAsync()).Single();
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Equal([LocalBackup.RoleKey("chef")], proposal.UndecidedKeys());
    }

    [Fact]
    public async Task UndoSelected_Should_ReturnEachItemToItsSource_When_Confirmed()
    {
        await SubmitProposalAsync();
        await Service<ChangeSets>().Wiki.Roles.SaveAsync(new LocalRole(Changed("imp"), null));
        var page = RenderAsAdmin();
        page.WaitForElements(".change-list > li");
        page.FindAll("button").Single(b => b.TextContent.StartsWith("Zatwierdź wszystko", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => Assert.Contains("Zatwierdzone, do wysłania (3)", page.Markup));

        page.FindAll(".wiki-undo button").Single(b => b.TextContent == "Zaznacz wszystkie").Click();
        var undo = () => page.FindAll(".wiki-undo button").Single(b => b.TextContent.Contains("cofnąć") || b.TextContent.Contains("Cofnij"));
        Assert.Contains("Cofnij zaznaczone (3)", undo().TextContent);
        undo().Click();
        Assert.Contains("Na pewno cofnąć: 3 zmiany?", undo().TextContent);
        undo().Click();

        page.WaitForAssertion(() => Assert.Contains("Wraca do „Czekają na akceptację”: 2 zmiany.", page.Find(".notice").TextContent));
        Assert.Contains("Usunięto zmiany wprowadzone bezpośrednio w wiki: 1 zmiana.", page.Find(".notice").TextContent);
        Assert.Empty((await Service<ChangeSets>().Wiki.GetAllAsync()).Keys());
        var proposal = (await Service<Proposals>().GetAllAsync()).Single();
        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Equal(2, proposal.UndecidedKeys().Count);
    }

    [Fact]
    public void EditingWikiSwitch_Should_SendEditorsToWiki_When_Ticked()
    {
        var page = RenderAsAdmin();
        var changes = Service<ChangeSets>();

        page.Find(".admin-section input[type=checkbox]").Change(true);

        page.WaitForAssertion(() => Assert.True(changes.EditingWiki));
        Assert.Same(changes.Wiki, changes.Current);
        Assert.Contains("zapisy w edytorach trafiają prosto do wiki", page.Find(".notice").TextContent);
    }

    [Fact]
    public void LeaveAdmin_Should_TurnModeOffAndGoHome_When_Clicked()
    {
        var page = RenderAsAdmin();

        page.Find(".admin-session__leave").Click();

        page.WaitForAssertion(() => Assert.False(Service<ChangeSets>().IsAdmin));
        Assert.Equal("http://localhost/", Service<NavigationManager>().Uri);
        Assert.False(Browser.Session.ContainsKey(AdminSession));
    }
}
