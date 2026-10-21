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
    public void FindRole_Should_IgnoreCase()
    {
        Assert.Equal("chef", Data.FindRole("CHEF")?.Id);
    }
}
