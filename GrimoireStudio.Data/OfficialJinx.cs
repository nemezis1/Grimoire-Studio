namespace GrimoireStudio.Data;

/// <summary>One published jinx of a role (botc-release, resources/data/jinxes.json).</summary>
public sealed record OfficialJinx
{
    public required string Id { get; init; }

    public string Reason { get; init; } = string.Empty;
}
