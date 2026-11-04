using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class ScriptAnalysisTests
{
    private static readonly GameData Data = GameData.Load(DataFolder.Path);

    private static ScriptAnalysis Analyze(params string[] ids) =>
        ScriptAnalysis.Analyze(Data, ids.Select(id => Data.FindRole(id)!).ToList());

    private static ScriptAnalysis AnalyzeScript(string id) =>
        ScriptAnalysis.Analyze(Data, Data.RolesOf(Data.FindScript(id)!));

    [Fact]
    public void Analyze_Should_CoverEveryPlayerCount_When_OfficialScript()
    {
        var analysis = AnalyzeScript("tb");

        Assert.Null(analysis.ShortFrom);
        Assert.False(analysis.IsUnplayable);
        Assert.Equal([13, 4, 4, 1], analysis.Teams.Select(t => t.Count));
    }

    [Fact]
    public void Analyze_Should_ReportFirstShortPlayerCount_When_TooFewMinions()
    {
        var analysis = Analyze("washerwoman", "librarian", "investigator", "chef", "empath", "fortuneteller", "butler", "poisoner", "imp");

        var minions = analysis.Teams.Single(t => t.Team == TeamIds.Minion);
        Assert.Equal(10, minions.ShortFrom);
        Assert.Equal(10, analysis.Teams.Single(t => t.Team == TeamIds.Townsfolk).ShortFrom);

        // 7 good roles: 6 players use 4 of them and leave 3 bluffs; 7 players use 5 and leave only 2.
        Assert.Equal(7, analysis.BluffsShortFrom);
        Assert.Equal(7, analysis.ShortFrom);
    }

    [Fact]
    public void Analyze_Should_BeUnplayable_When_NoDemon()
    {
        Assert.True(Analyze("chef", "poisoner").IsUnplayable);
    }

    [Fact]
    public void Analyze_Should_ListRequiredPartner_When_HuntsmanWithoutDamsel()
    {
        var analysis = Analyze("huntsman", "imp");

        Assert.Equal([("huntsman", "damsel")], analysis.Missing.Select(m => (m.Role.Id, m.Needed.Id)));
        Assert.Empty(Analyze("huntsman", "damsel", "imp").Missing);
    }

    [Fact]
    public void Analyze_Should_FindJinxesAndAskForDjinn_When_JinxedRolesTogether()
    {
        var pair = Data.Jinxes.First(o => o.Jinxes.Any(j => Data.FindRole(j.Id) is not null) && Data.FindRole(o.Id) is not null);
        var other = pair.Jinxes.First(j => Data.FindRole(j.Id) is not null).Id;

        var analysis = Analyze(pair.Id, other);

        Assert.Contains(analysis.Jinxes, j => j.Owner.Id == pair.Id && j.Other.Id == other);
        Assert.Contains(analysis.Missing, m => m.Needed.Id == ScriptAnalysis.JinxFabled);
        Assert.DoesNotContain(Analyze(pair.Id, other, ScriptAnalysis.JinxFabled).Missing, m => m.Needed.Id == ScriptAnalysis.JinxFabled);
    }
}
