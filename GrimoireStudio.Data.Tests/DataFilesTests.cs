using System.Text;
using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class DataFilesTests
{
    // 1×1 transparent PNG.
    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    private static readonly GameData Data = GameData.Load(DataFolder.Path);

    [Fact]
    public void Serialize_Should_ReproduceRepositoryFiles()
    {
        foreach (var (name, json) in DataFiles.Serialize(Data))
        {
            // Git may check the files out with CRLF.
            var onDisk = File.ReadAllText(Path.Combine(DataFolder.Path, name)).ReplaceLineEndings("\n");
            Assert.True(onDisk == json, $"{name} is not in the canonical form; write it with DataFiles.");
        }
    }

    [Fact]
    public void ChangedFiles_Should_BeEmpty_When_NothingChanged()
    {
        Assert.Empty(RepositoryExport.ChangedFiles(Data, new LocalBackup()));
    }

    [Fact]
    public void ChangedFiles_Should_WriteOnlyTheChangedLines_When_RoleChanged()
    {
        var chef = Data.FindRole("chef")!;
        var changes = new LocalBackup { Roles = [new LocalRole(chef with { NamePl = "Szef kuchni" }, Png)] };

        var files = RepositoryExport.ChangedFiles(Data, changes);

        Assert.Equal(["icons/chef.png", GameData.RolesFile], files.Select(f => f.Path));
        var before = DataFiles.ToJson(Data.Roles).Split('\n');
        var after = Encoding.UTF8.GetString(files[1].Content).Split('\n');
        Assert.Equal(before.Length, after.Length);
        Assert.Single(before.Zip(after), pair => pair.First != pair.Second);
    }

    [Fact]
    public void ChangedFiles_Should_SaveScriptPictureAsEditionIcon()
    {
        var script = new Script { Id = "nowy", Name = "Nowy", Roles = ["chef"], CustomIcon = "custom", Image = Png };

        var files = RepositoryExport.ChangedFiles(Data, new LocalBackup { Scripts = [script] });

        Assert.Equal(["editions/nowy.png", GameData.ScriptsFile], files.Select(f => f.Path));
        var saved = Assert.Single(ReadScripts(files[1]).Scripts, s => s.Id == "nowy");
        Assert.Null(saved.Image);
        Assert.Equal("nowy", saved.IconName);
    }

    [Fact]
    public void ChangedFiles_Should_WriteTeamsAndJinxes()
    {
        var changes = new LocalBackup
        {
            Teams = [new TeamInfo { Id = "loric", NamePl = "Loric", NameEn = "Loric", PluralPl = "Loric", PluralEn = "Loric", Order = 55 }],
            Jinxes = [new JinxOverride { OwnerId = "chef", Jinx = new Jinx { Id = "imp", ReasonEn = "x", ReasonPl = "y" } }],
        };

        var files = RepositoryExport.ChangedFiles(Data, changes);

        Assert.Equal([GameData.JinxesFile, GameData.TeamsFile], files.Select(f => f.Path).Order());
    }

    [Fact]
    public void ChangedFiles_Should_Reject_When_IconIsNotPng()
    {
        var chef = Data.FindRole("chef")!;
        var changes = new LocalBackup { Roles = [new LocalRole(chef, "data:image/jpeg;base64,/9j/4AAQ")] };

        Assert.Throws<InvalidDataException>(() => RepositoryExport.ChangedFiles(Data, changes));
    }

    [Fact]
    public void ChangedFiles_Should_Reject_When_ChangesBreakData()
    {
        var script = new Script { Id = "zly", Name = "Zły", Roles = ["nieistnieje"] };

        var error = Assert.Throws<InvalidDataException>(() => RepositoryExport.ChangedFiles(Data, new LocalBackup { Scripts = [script] }));

        Assert.Contains("unknown role 'nieistnieje'", error.Message);
    }

    [Fact]
    public void ChangedFiles_Should_Reject_When_NewRoleHasNoIcon()
    {
        var role = Data.FindRole("chef")! with { Id = "nowarola" };

        var error = Assert.Throws<InvalidDataException>(() => RepositoryExport.ChangedFiles(Data, new LocalBackup { Roles = [new LocalRole(role, null)] }));

        Assert.Contains("needs an icon", error.Message);
    }

    private static ScriptCatalog ReadScripts(DataFile file) =>
        System.Text.Json.JsonSerializer.Deserialize<ScriptCatalog>(file.Content, GameData.JsonOptions)!;
}
