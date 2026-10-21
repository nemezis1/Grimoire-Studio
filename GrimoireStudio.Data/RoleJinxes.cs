using System.Text.Json.Serialization;

namespace GrimoireStudio.Data;

/// <summary>One entry of hatred.json: all jinxes owned by role <see cref="Id"/>.</summary>
public sealed record RoleJinxes
{
    public required string Id { get; init; }

    [JsonPropertyName("hatred")]
    public IReadOnlyList<Jinx> Jinxes { get; init; } = [];
}
