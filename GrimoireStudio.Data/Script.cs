using System.Text.Json.Serialization;

namespace GrimoireStudio.Data;

public sealed record Script
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Role ids (base roles or fabled) on this script.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool IsOfficial { get; init; }

    /// <summary>Edition icon name to use instead of <see cref="Id"/>, e.g. "custom".</summary>
    public string CustomIcon { get; init; } = string.Empty;

    /// <summary>Uploaded picture (data URL) shown instead of the edition icon; scripts from the data folder have none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Image { get; init; }

    [JsonIgnore]
    public string IconName => string.IsNullOrWhiteSpace(CustomIcon) ? Id : CustomIcon;
}
