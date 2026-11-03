using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class LocalBackupTests
{
    private static readonly GameData Data = GameData.Load(DataFolder.Path);

    [Fact]
    public void As_Should_KeepItemsAsTheyAreInData()
    {
        var role = Data.Roles[0];
        var owner = Data.Jinxes[0];
        var jinx = owner.Jinxes[0];
        var changes = new LocalBackup
        {
            Roles = [new LocalRole(role with { AbilityPl = "zmieniona" }, null), new LocalRole(role with { Id = "nowa_rola" }, null)],
            Jinxes = [new JinxOverride { OwnerId = owner.Id, Jinx = jinx with { ReasonPl = "zmieniony" } }],
        };

        var before = changes.As(Data);

        Assert.Equal(role, Assert.Single(before.Roles).Role);
        Assert.Equal(jinx, Assert.Single(before.Jinxes).Jinx);
    }

    [Fact]
    public void As_Should_GiveComparableData()
    {
        var role = Data.Roles[0];
        var changes = new LocalBackup { Roles = [new LocalRole(role with { AbilityPl = "zmieniona" }, null)] };

        var before = changes.As(Data).ApplyTo(new GameData());

        Assert.Equal(role.AbilityPl, before.FindRole(role.Id)?.AbilityPl);
        Assert.Null(before.FindRole("nowa_rola"));
    }
}
