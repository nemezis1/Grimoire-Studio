using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class DataValidatorTests
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

    private static GameData CreateData(
        IReadOnlyList<Role>? roles = null,
        IReadOnlyList<Script>? scripts = null,
        IReadOnlyList<RoleJinxes>? jinxes = null) => new()
    {
        Roles = roles ?? [CreateRole("chef"), CreateRole("imp", TeamIds.Demon)],
        Fabled = [CreateRole("djinn", TeamIds.Fabled)],
        Scripts = new ScriptCatalog { Scripts = scripts ?? [new Script { Id = "tb", Name = "TB", Roles = ["chef", "imp", "djinn"] }] },
        Jinxes = jinxes ?? [],
    };

    [Fact]
    public void Validate_Should_ReturnNoErrors_When_DataIsConsistent()
    {
        Assert.Empty(DataValidator.Validate(CreateData()));
    }

    [Fact]
    public void Validate_Should_ReportDuplicate_When_RoleIdRepeats()
    {
        var errors = DataValidator.Validate(CreateData(roles: [CreateRole("chef"), CreateRole("chef")]));

        Assert.Contains(errors, e => e.Contains("duplicate id"));
    }

    [Theory]
    [InlineData("Pit-Hag")]
    [InlineData("scarlet woman")]
    public void Validate_Should_ReportBadId_When_IdIsNotNormalized(string id)
    {
        var errors = DataValidator.Validate(CreateData(roles: [CreateRole(id)], scripts: []));

        Assert.Contains(errors, e => e.Contains("lowercase"));
    }

    [Fact]
    public void Validate_Should_ReportUnknownRole_When_ScriptReferencesMissingRole()
    {
        var script = new Script { Id = "nz", Name = "NZ", Roles = ["chef", "mephit"] };

        var errors = DataValidator.Validate(CreateData(scripts: [script]));

        Assert.Contains(errors, e => e.Contains("unknown role 'mephit'"));
    }

    [Fact]
    public void Validate_Should_ReportUnknownRole_When_JinxReferencesMissingRole()
    {
        var jinxes = new[]
        {
            new RoleJinxes { Id = "chef", Jinxes = [new Jinx { Id = "Pit-Hag", ReasonEn = "a", ReasonPl = "b" }] },
        };

        var errors = DataValidator.Validate(CreateData(jinxes: jinxes));

        Assert.Contains(errors, e => e.Contains("'Pit-Hag': unknown role"));
    }

    [Fact]
    public void Validate_Should_ReportMismatch_When_ReminderCountsDiffer()
    {
        var role = CreateRole("chef") with { RemindersEn = ["Dead"], RemindersPl = [] };

        var errors = DataValidator.Validate(CreateData(roles: [role], scripts: []));

        Assert.Contains(errors, e => e.Contains("EN reminders"));
    }

    [Fact]
    public void Validate_Should_ReportMisplacedFabled_When_FabledIsInRoles()
    {
        var errors = DataValidator.Validate(CreateData(roles: [CreateRole("spirit", TeamIds.Fabled)], scripts: []));

        Assert.Contains(errors, e => e.Contains("fabled belongs in"));
    }
}
