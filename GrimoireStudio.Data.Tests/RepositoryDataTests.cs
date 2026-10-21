using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

/// <summary>Guards the committed data files: any broken edit fails CI before it reaches the site.</summary>
public class RepositoryDataTests
{
    private static readonly GameData Data = GameData.Load(DataFolder.Path);

    [Fact]
    public void Load_Should_ReadAllFiles()
    {
        Assert.NotEmpty(Data.Roles);
        Assert.NotEmpty(Data.Fabled);
        Assert.NotEmpty(Data.Scripts.Scripts);
        Assert.NotEmpty(Data.Jinxes);
    }

    [Fact]
    public void Validate_Should_ReportNoErrors()
    {
        var errors = DataValidator.Validate(Data, DataFolder.Icons);

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void Icons_Should_BelongToRoles()
    {
        var ids = Data.AllRoles.Select(r => r.Id).ToHashSet();
        var orphans = Directory.GetFiles(DataFolder.Icons, "*.png")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !ids.Contains(name!))
            .ToList();

        Assert.True(orphans.Count == 0, "Icons without a role: " + string.Join(", ", orphans));
    }

    [Fact]
    public void Scripts_Should_HaveEditionIcon()
    {
        var editions = Path.Combine(DataFolder.Path, "editions");
        var missing = Data.Scripts.Scripts
            .Where(s => !File.Exists(Path.Combine(editions, $"{s.IconName}.png")))
            .Select(s => s.Id)
            .ToList();

        Assert.True(missing.Count == 0, "Scripts without edition icon: " + string.Join(", ", missing));
    }
}
