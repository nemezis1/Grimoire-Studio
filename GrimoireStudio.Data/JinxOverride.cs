using System.Text.Json.Serialization;

namespace GrimoireStudio.Data;

/// <summary>
/// A jinx changed in the browser: replaces (or adds) the jinx <see cref="Jinx"/> owned by <see cref="OwnerId"/>,
/// or removes it when <see cref="Removed"/> is set.
/// </summary>
public sealed record JinxOverride
{
    public required string OwnerId { get; init; }
    public required Jinx Jinx { get; init; }
    public bool Removed { get; init; }

    /// <summary>Identity of the pair: owner and other role.</summary>
    [JsonIgnore]
    public string Key => $"{OwnerId}/{Jinx.Id}".ToLowerInvariant();
}
