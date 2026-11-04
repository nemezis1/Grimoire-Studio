using Bunit;
using Grimoire_Studio.Components;
using GrimoireStudio.Data;
using Microsoft.AspNetCore.Components;

namespace GrimoireStudio.App.Tests;

public class ChangeSetViewTests : AppTestContext
{
    private readonly GameData _data;
    private readonly Role _chef;

    public ChangeSetViewTests()
    {
        _data = LoadData();
        _chef = _data.FindRole("chef")!;
    }

    private Role ChangedChef => _chef with { AbilityPl = "Nowa zdolność kucharza." };

    private static Role NewRole => new()
    {
        Id = "testrole",
        NameEn = "Test Role",
        NamePl = "Rola Testowa",
        AbilityEn = "Test.",
        AbilityPl = "Test.",
    };

    private LocalBackup Changes => new()
    {
        Roles = [new LocalRole(ChangedChef, null), new LocalRole(NewRole, null)],
        Removed = [LocalBackup.RoleKey("imp")],
    };

    private IRenderedComponent<ChangeSetView> Render(LocalBackup changes, Action<ComponentParameterCollectionBuilder<ChangeSetView>>? more = null) =>
        Render<ChangeSetView>(p =>
        {
            p.Add(x => x.Changes, changes).Add(x => x.Base, _data).Add(x => x.Data, changes.ApplyTo(_data));
            more?.Invoke(p);
        });

    private static AngleSharp.Dom.IElement Item(IRenderedComponent<ChangeSetView> view, string text) =>
        view.FindAll(".change-list > li").Single(li => li.TextContent.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void Render_Should_ShowChildContent_When_NoChanges()
    {
        var view = Render(new LocalBackup(), p => p.AddChildContent("<p class=\"empty\">Brak zmian</p>"));

        Assert.Equal("Brak zmian", view.Find(".empty").TextContent);
        Assert.Empty(view.FindAll(".change-group"));
    }

    [Fact]
    public void Render_Should_GroupItemsAndMarkNewOrChanged_When_ChangesGiven()
    {
        var view = Render(Changes);

        var groups = view.FindAll(".change-group > summary h3").Select(h => h.TextContent).ToList();
        Assert.Equal(["Role (2)", "Usunięte (1)"], groups);
        Assert.Contains("zmieniona", Item(view, _chef.NamePl).QuerySelector(".local-badge")!.TextContent);
        Assert.Contains("nowa", Item(view, NewRole.NamePl).QuerySelector(".local-badge")!.TextContent);

        // A changed role shows the before / after of what changed.
        var details = Item(view, _chef.NamePl).QuerySelector(".change-details")!;
        Assert.Contains(_chef.AbilityPl, details.QuerySelector(".change-details__before")!.TextContent);
        Assert.Contains(ChangedChef.AbilityPl, details.TextContent);
    }

    [Fact]
    public void Render_Should_ListScriptsLosingTheRole_When_RoleRemoved()
    {
        var view = Render(Changes);

        var removed = Item(view, _data.FindRole("imp")!.NamePl);
        Assert.Contains("usunięta", removed.QuerySelector(".local-badge")!.TextContent);
        Assert.Contains("znika też ze skryptów", removed.TextContent);
        Assert.Contains(_data.FindScript("tb")!.Name, removed.TextContent);
    }

    [Fact]
    public void Checkbox_Should_UpdateSelectionAndNotify_When_Unticked()
    {
        var selected = Changes.Keys().ToHashSet();
        var notified = 0;
        var view = Render(Changes, p => p
            .Add(x => x.Selected, selected)
            .Add(x => x.SelectedChanged, EventCallback.Factory.Create(this, () => notified++)));

        Item(view, _chef.NamePl).QuerySelector("input[type=checkbox]")!.Change(false);

        Assert.DoesNotContain(LocalBackup.RoleKey("chef"), selected);
        Assert.Equal(2, selected.Count);
        Assert.Equal(1, notified);
    }

    [Fact]
    public void RemoveButton_Should_SayRemoveForNewAndRestoreForChanged_When_OnRemoveSet()
    {
        var removed = new List<string>();
        var view = Render(Changes, p => p.Add(x => x.OnRemove, (string key) => removed.Add(key)));

        Assert.Equal("Przywróć", Item(view, _chef.NamePl).QuerySelector("button")!.TextContent);
        var remove = Item(view, NewRole.NamePl).QuerySelector("button")!;
        Assert.Equal("Usuń", remove.TextContent);

        remove.Click();

        Assert.Equal([LocalBackup.RoleKey(NewRole.Id)], removed);
    }

    [Fact]
    public void Render_Should_ShowActionsTagsAndOrigins_When_Given()
    {
        var view = Render(Changes, p => p
            .Add(x => x.Actions, (string key) => $"<button class=\"act\">{key}</button>")
            .Add(x => x.Tags, new Dictionary<string, string> { [LocalBackup.RoleKey("chef")] = "wysłane do akceptacji" })
            .Add(x => x.Origins, new Dictionary<string, string> { [LocalBackup.RoleKey("chef")] = "zgłoszenie: Ala" }));

        Assert.Equal(Changes.Keys(), view.FindAll("button.act").Select(b => b.TextContent));

        var chef = Item(view, _chef.NamePl);
        Assert.Equal("wysłane do akceptacji", chef.QuerySelector(".local-badge--pending")!.TextContent);
        Assert.Equal("zgłoszenie: Ala", chef.QuerySelector(".change-origin")!.TextContent);
        Assert.Equal("zmiana bezpośrednio w wiki", Item(view, NewRole.NamePl).QuerySelector(".change-origin")!.TextContent);
    }
}
