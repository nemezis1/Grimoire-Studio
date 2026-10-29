using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class GameDataQueriesTests
{
    private static Role CreateRole(string id, int firstNight = 0, int otherNight = 0) => new()
    {
        Id = id,
        NameEn = id,
        NamePl = id,
        AbilityEn = "ability",
        AbilityPl = "zdolność",
        FirstNight = firstNight,
        OtherNight = otherNight,
    };

    private static readonly Script Tb = new() { Id = "tb", Name = "TB", Roles = ["poisoner", "chef", "imp", "monk"] };
    private static readonly Script Other = new() { Id = "x", Name = "X", Roles = ["chef", "spy"] };

    private static readonly GameData Data = new()
    {
        Teams = DataFolder.Teams,
        Roles =
        [
            CreateRole("chef", firstNight: 30),
            CreateRole("poisoner", firstNight: 10, otherNight: 10),
            CreateRole("imp", otherNight: 50),
            CreateRole("monk", otherNight: 20),
            CreateRole("spy", firstNight: 90, otherNight: 90),
        ],
        Scripts = new ScriptCatalog { Scripts = [Tb, Other] },
        Jinxes =
        [
            new RoleJinxes { Id = "spy", Jinxes = [new Jinx { Id = "chef", ReasonEn = "a", ReasonPl = "a" }] },
            new RoleJinxes { Id = "chef", Jinxes = [new Jinx { Id = "imp", ReasonEn = "b", ReasonPl = "b" }] },
        ],
    };

    [Fact]
    public void NightOrder_Should_ReturnWakingRolesSorted_When_FirstNight()
    {
        var order = Data.NightOrder(Tb, firstNight: true).Select(r => r.Id);

        Assert.Equal(["poisoner", "chef"], order);
    }

    [Fact]
    public void NightOrder_Should_ReturnWakingRolesSorted_When_OtherNights()
    {
        var order = Data.NightOrder(Tb, firstNight: false).Select(r => r.Id);

        Assert.Equal(["poisoner", "monk", "imp"], order);
    }

    [Fact]
    public void NightSteps_Should_PlaceMinionAndDemonInfo_When_FirstNight()
    {
        var steps = Data.NightSteps(Tb, firstNight: true)
            .Select(s => s.Role?.Id ?? s.Kind.ToString());

        // poisoner 10, chef 30, minion info 60, demon info 90
        Assert.Equal(["poisoner", "chef", "MinionInfo", "DemonInfo"], steps);
    }

    [Fact]
    public void NightSteps_Should_SkipInfo_When_OtherNightsOrDisabled()
    {
        Assert.DoesNotContain(Data.NightSteps(Tb, firstNight: false), s => s.Role is null);
        Assert.DoesNotContain(Data.NightSteps(Tb, firstNight: true, includeInfo: false), s => s.Role is null);
    }

    [Fact]
    public void ScriptsWithRole_Should_ReturnEveryScriptListingRole()
    {
        Assert.Equal(["tb", "x"], Data.ScriptsWithRole("chef").Select(s => s.Id));
    }

    [Fact]
    public void JinxesFor_Should_ReturnBothDirections()
    {
        var others = Data.JinxesFor("chef").Select(j => j.Other.Id).Order();

        Assert.Equal(["imp", "spy"], others);
    }

    [Fact]
    public void JinxesFor_Should_SkipRolesOutsideScope_When_ScopeGiven()
    {
        var others = Data.JinxesFor("chef", Tb).Select(j => j.Other.Id);

        Assert.Equal(["imp"], others);
    }

    [Fact]
    public void WithScripts_Should_ReplaceExistingAndAppendNew()
    {
        var edited = new Script { Id = "tb", Name = "TB (moja wersja)" };
        var added = new Script { Id = "nowy", Name = "Nowy" };

        var merged = Data.WithScripts([added, edited]).Scripts.Scripts;

        Assert.Equal(["tb", "x", "nowy"], merged.Select(s => s.Id));
        Assert.Equal("TB (moja wersja)", merged[0].Name);
    }

    [Fact]
    public void WithRoles_Should_ReplaceExistingAndAppendNew()
    {
        var edited = CreateRole("chef") with { NamePl = "Kucharz (mój)" };
        var added = CreateRole("baker");

        var merged = Data.WithRoles([added, edited]);

        Assert.Equal("Kucharz (mój)", merged.FindRole("chef")?.NamePl);
        Assert.Equal("baker", merged.Roles[^1].Id);
        Assert.Equal(Data.Roles.Count + 1, merged.Roles.Count);
    }

    [Fact]
    public void WithRoles_Should_PutFabledInFabled()
    {
        var merged = Data.WithRoles([CreateRole("spirit") with { Team = TeamIds.Fabled }]);

        Assert.Contains(merged.Fabled, r => r.Id == "spirit");
        Assert.DoesNotContain(merged.Roles, r => r.Id == "spirit");
    }

    [Fact]
    public void FindRole_Should_IgnoreCase()
    {
        Assert.Equal("chef", Data.FindRole("CHEF")?.Id);
    }

    [Fact]
    public void WithTeams_Should_ReplaceBuiltinAndAddNewInOrder()
    {
        var merged = Data.WithTeams(
        [
            Data.Teams[0] with { PluralPl = "Mieszczanie" },
            new TeamInfo { Id = "loric", NamePl = "Loric", NameEn = "Loric", PluralPl = "Loric", PluralEn = "Loric", Order = 55 },
        ]);

        Assert.Equal("Mieszczanie", merged.FindTeam(TeamIds.Townsfolk)?.PluralPl);
        Assert.Equal([TeamIds.Townsfolk, TeamIds.Outsider, TeamIds.Minion, TeamIds.Demon, TeamIds.Traveler, "loric", TeamIds.Fabled], merged.Teams.Select(t => t.Id));
    }

    [Fact]
    public void TeamOf_Should_ReturnStandIn_When_TeamIsUndefined()
    {
        var team = Data.TeamOf(CreateRole("x") with { Team = "gone" });

        Assert.Equal("gone", team.PluralPl);
    }

    [Fact]
    public void WithJinxes_Should_ChangeAddAndRemovePairs()
    {
        var merged = Data.WithJinxes(
        [
            new JinxOverride { OwnerId = "chef", Jinx = new Jinx { Id = "imp", ReasonEn = "new", ReasonPl = "nowy" } },
            new JinxOverride { OwnerId = "spy", Jinx = new Jinx { Id = "chef" }, Removed = true },
            new JinxOverride { OwnerId = "monk", Jinx = new Jinx { Id = "imp", ReasonEn = "c", ReasonPl = "c" } },
        ]);

        Assert.Equal("nowy", Assert.Single(merged.JinxesFor("chef")).Jinx.ReasonPl);
        Assert.Equal(2, merged.JinxesFor("imp").Count);
        Assert.DoesNotContain(merged.Jinxes, o => o.Id == "spy");
    }
}
