using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class ItemConflictsTests
{
    private static readonly GameData Data = GameData.Load(DataFolder.Path);

    [Fact]
    public void Find_Should_ReportItemsChangedOnBothSides()
    {
        var (first, second) = (Data.Roles[0], Data.Roles[1]);
        var now = Data.WithRoles([first with { AbilityPl = "ich" }, second with { AbilityPl = "ich" }]);
        var changes = new LocalBackup
        {
            Roles = [new LocalRole(first with { AbilityPl = "moja" }, null), new LocalRole(second with { AbilityPl = "ich" }, null)],
        };

        var conflicts = ItemConflicts.Find(Data, now, changes);

        Assert.Equal([LocalBackup.RoleKey(first.Id)], conflicts);
    }

    [Fact]
    public void Find_Should_IgnoreItemsChangedOnlyByOthersOrOnlyHere()
    {
        var (first, second) = (Data.Roles[0], Data.Roles[1]);
        var owner = Data.Jinxes[0];
        var now = Data.WithRoles([second with { AbilityPl = "ich" }]);
        var changes = new LocalBackup
        {
            Roles = [new LocalRole(first with { AbilityPl = "moja" }, null)],
            Jinxes = [new JinxOverride { OwnerId = owner.Id, Jinx = owner.Jinxes[0] with { ReasonPl = "mój" } }],
        };

        Assert.Empty(ItemConflicts.Find(Data, now, changes));
    }

    [Fact]
    public void Json_Should_FindJinxesByOwnerAndOtherRole()
    {
        var owner = Data.Jinxes[0];
        var jinx = owner.Jinxes[0];

        Assert.Equal(DataFiles.ToJson(jinx), ItemConflicts.Json(Data, $"jinx:{owner.Id}/{jinx.Id}"));
        Assert.Null(ItemConflicts.Json(Data, "jinx:nikt/nic"));
        Assert.Null(ItemConflicts.Json(Data, "role:nikt"));
    }
}
