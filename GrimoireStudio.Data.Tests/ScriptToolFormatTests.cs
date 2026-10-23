using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class ScriptToolFormatTests
{
    private static Role CreateRole(string id, string team = TeamIds.Townsfolk) => new()
    {
        Id = id,
        NameEn = id,
        NamePl = id,
        AbilityEn = "ability",
        AbilityPl = "zdolność",
        Team = team,
    };

    private static readonly GameData Data = new()
    {
        Roles = [CreateRole("fortuneteller"), CreateRole("chef"), CreateRole("imp", TeamIds.Demon)],
    };

    [Fact]
    public void Import_Should_ReadMetaAndNormalizeIds()
    {
        const string json = """[{"id": "_meta", "name": "Mój skrypt"}, "fortune_teller", {"id": "chef"}, "Imp", "chef"]""";

        var result = ScriptToolFormat.Import(json, Data, "moj");

        Assert.Equal("Mój skrypt", result.Script.Name);
        Assert.Equal(["fortuneteller", "chef", "imp"], result.Script.Roles);
        Assert.Empty(result.UnknownRoles);
    }

    [Fact]
    public void Import_Should_ReportUnknownIds()
    {
        var result = ScriptToolFormat.Import("""["chef", "mephit"]""", Data, "x");

        Assert.Equal(["mephit"], result.UnknownRoles);
        Assert.Equal(["chef"], result.Script.Roles);
    }

    [Fact]
    public void Import_Should_CreateHomebrewRoleTeamAndJinx()
    {
        const string json = """
            [{"id": "big_wig", "name": "Big Wig", "team": "loric", "ability": "Does things.", "firstNight": 12.4,
              "reminders": ["Wig"], "setup": true, "jinxes": [{"id": "chef", "reason": "Because."}]},
             {"id": "hermit", "name": "Hermit", "team": "traveller", "ability": "Hides."}]
            """;

        var result = ScriptToolFormat.Import(json, Data, "x");

        var wig = result.NewRoles[0];
        Assert.Equal(("bigwig", "Big Wig", "loric", 12, true), (wig.Id, wig.NamePl, wig.Team, wig.FirstNight, wig.Setup));
        Assert.Equal(["Wig"], wig.RemindersPl);
        Assert.Equal(TeamIds.Traveler, result.NewRoles[1].Team);
        Assert.Equal("loric", Assert.Single(result.NewTeams).Id);
        Assert.Equal("bigwig/chef", Assert.Single(result.NewJinxes).Key);
    }

    [Fact]
    public void Import_Should_Throw_When_NotAnArray()
    {
        Assert.Throws<FormatException>(() => ScriptToolFormat.Import("""{"id": "chef"}""", Data, "x"));
        Assert.Throws<FormatException>(() => ScriptToolFormat.Import("not json", Data, "x"));
    }

    [Fact]
    public void Export_Should_WriteIdsAndFullLocalRoles()
    {
        var local = CreateRole("bigwig") with { NameEn = "Big Wig" };
        var data = Data.WithRoles([local]);
        var script = new Script { Id = "x", Name = "X", Roles = ["chef", "bigwig"] };

        var json = ScriptToolFormat.Export(script, data, Data);
        var back = ScriptToolFormat.Import(json, Data, "x");

        Assert.Equal("X", back.Script.Name);
        Assert.Equal(["chef", "bigwig"], back.Script.Roles);
        Assert.Equal("Big Wig", Assert.Single(back.NewRoles).NameEn);
    }
}
