using Bunit;
using Grimoire_Studio.Components;
using Grimoire_Studio.Pages;
using GrimoireStudio.Data;
using Microsoft.AspNetCore.Components;

namespace GrimoireStudio.App.Tests;

/// <summary>The dropdown filters shared by the role list and the script editor.</summary>
public class FilterTests : AppTestContext
{
    private readonly GameData _data;

    public FilterTests()
    {
        _data = LoadData();
    }

    [Fact]
    public void ScriptFilter_Should_ListOfficialScriptsAboveALine_When_Opened()
    {
        var filter = Render<ScriptFilter>(p => p
            .Add(f => f.Scripts, _data.Scripts.Scripts)
            .Add(f => f.Ticked, new HashSet<string>()));

        var names = filter.FindAll(".multi-select__menu label").Select(l => l.TextContent.Trim()).ToList();
        var official = _data.Scripts.Scripts.Where(s => s.IsOfficial).Select(s => s.Name).ToList();
        Assert.NotEmpty(official);
        Assert.Equal(official, names.Take(official.Count));
        Assert.Single(filter.FindAll("hr.multi-select__separator"));
        Assert.Equal(_data.Scripts.Scripts.Count, filter.FindAll(".multi-select__icon").Count);
    }

    [Fact]
    public void ScriptEditor_Should_ListOnlyTheTickedTeam_When_TeamFiltered()
    {
        Service<NavigationManager>().NavigateTo("scripts/new");
        var editor = Render<ScriptEditor>();
        editor.WaitForElements(".role-picker > li");

        var teams = editor.FindAll("details.multi-select").Single(d => d.QuerySelector("summary")!.TextContent.Contains("Wszystkie drużyny"));
        teams.QuerySelectorAll("label").First(l => l.TextContent.Contains("Demony")).QuerySelector("input")!.Change(true);

        var demons = _data.AllRoles.Count(r => r.Team == TeamIds.Demon);
        editor.WaitForAssertion(() => Assert.Equal(demons, editor.FindAll(".role-picker > li").Count));
    }
}
