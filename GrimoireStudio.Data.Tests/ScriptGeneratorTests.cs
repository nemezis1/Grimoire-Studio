using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class ScriptGeneratorTests
{
    private static readonly GameData Data = GameData.Load(DataFolder.Path);

    private static readonly IReadOnlyDictionary<string, int> Usual = ScriptGenerator.Usual.ToDictionary(t => t.Team, t => t.Count);

    private static Role Role(string id) => Data.FindRole(id)!;

    [Fact]
    public void Fill_Should_KeepChosenRolesAndReachTheWantedCounts_When_PoolIsLarge()
    {
        var chosen = new[] { Role("imp"), Role("chef"), Role("baron") };

        var added = ScriptGenerator.Fill(Data, chosen, Data.AllRoles, Usual, new Random(1));

        var script = chosen.Concat(added).ToList();
        Assert.Equal(script.Count, script.Select(r => r.Id).Distinct().Count());
        Assert.DoesNotContain(added, r => chosen.Any(c => c.Id == r.Id));
        foreach (var (team, count) in ScriptGenerator.Usual)
        {
            Assert.Equal(count, script.Count(r => r.Team == team));
        }

        Assert.DoesNotContain(script, r => r.Team is TeamIds.Fabled or TeamIds.Traveler);
    }

    [Fact]
    public void Fill_Should_LeaveAFullTeamAsItIs_When_ItHasEnoughAlready()
    {
        var demons = Data.AllRoles.Where(r => r.Team == TeamIds.Demon).Take(5).ToList();

        var added = ScriptGenerator.Fill(Data, demons, Data.AllRoles, Usual, new Random(2));

        Assert.DoesNotContain(added, r => r.Team == TeamIds.Demon);
    }

    [Fact]
    public void Fill_Should_DrawOnlyFromThePool_When_PoolIsLimited()
    {
        var pool = Data.RolesOf(Data.FindScript("tb")!);

        var added = ScriptGenerator.Fill(Data, [], pool, Usual, new Random(3));

        Assert.All(added, r => Assert.Contains(pool, p => p.Id == r.Id));
        Assert.Equal(pool.Count(r => Usual.ContainsKey(r.Team)), added.Count);
    }

    [Fact]
    public void Fill_Should_AddThePartnerWithARoleThatNeedsOne_When_Drawn()
    {
        // Only the Huntsman and the Damsel to draw from: the Huntsman comes with the Damsel, never alone.
        var pool = new[] { Role("huntsman"), Role("damsel") };

        for (var seed = 0; seed < 10; seed++)
        {
            var added = ScriptGenerator.Fill(Data, [], pool, Usual, new Random(seed));
            Assert.Contains(added, r => r.Id == "damsel");
        }

        var noRoomForDamsel = new Dictionary<string, int>(Usual) { [TeamIds.Outsider] = 0 };
        var alone = ScriptGenerator.Fill(Data, [], pool, noRoomForDamsel, new Random(4));
        Assert.DoesNotContain(alone, r => r.Id == "huntsman");
    }
}
