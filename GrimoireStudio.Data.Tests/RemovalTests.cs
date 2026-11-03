using System.Text;
using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class RemovalTests
{
    private static readonly GameData Data = GameData.Load(DataFolder.Path);

    private static Role RoleInScriptAndJinx() =>
        Data.Roles.First(r => Data.ScriptsWithRole(r.Id).Any() && Data.JinxesFor(r.Id).Count > 0);

    [Fact]
    public void ApplyTo_Should_RemoveRoleFromScriptsAndJinxes()
    {
        var role = RoleInScriptAndJinx();
        var changes = new LocalBackup { Removed = [LocalBackup.RoleKey(role.Id)] };

        var updated = changes.ApplyTo(Data);

        Assert.Null(updated.FindRole(role.Id));
        Assert.Empty(updated.ScriptsWithRole(role.Id));
        Assert.Empty(updated.JinxesFor(role.Id));
        Assert.Equal(Data.Scripts.Scripts.Count, updated.Scripts.Scripts.Count);
        Assert.Empty(DataValidator.Validate(updated).Except(DataValidator.Validate(Data)));
    }

    [Fact]
    public void ChangedFiles_Should_DeleteTheIconOfARemovedRole()
    {
        var role = RoleInScriptAndJinx();
        var files = RepositoryExport.ChangedFiles(Data, new LocalBackup { Removed = [LocalBackup.RoleKey(role.Id)] });

        var icon = Assert.Single(files, f => f.Path == $"{GameData.IconsFolder}/{role.Id}.png");
        Assert.True(icon.Deleted);
        Assert.Contains(files, f => f.Path == GameData.RolesFile || f.Path == GameData.FabledFile);
        Assert.Contains(files, f => f.Path == GameData.ScriptsFile);
    }

    [Fact]
    public void Keys_Only_And_As_Should_IncludeRemovedItems()
    {
        var script = Data.Scripts.Scripts[0];
        var changes = new LocalBackup { Removed = [LocalBackup.ScriptKey(script.Id)] };

        Assert.Equal([LocalBackup.ScriptKey(script.Id)], changes.Keys());
        Assert.Empty(changes.Only(new HashSet<string>()).Removed);
        Assert.Equal(script, Assert.Single(changes.As(Data).Scripts));
        Assert.Null(ItemConflicts.Json(changes.ApplyTo(Data), LocalBackup.ScriptKey(script.Id)));
    }

    [Fact]
    public void ExperimentalFlag_Should_BeWrittenOnlyWhenSet()
    {
        var role = Data.Roles[0];

        Assert.DoesNotContain("isExperimental", DataFiles.ToJson(role with { IsExperimental = false }));
        Assert.Contains("\"isExperimental\": true", DataFiles.ToJson(role with { IsExperimental = true }));
    }
}
