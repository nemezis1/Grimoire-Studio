namespace GrimoireStudio.Data;

/// <summary>A role's published jinxes (one entry of botc-release's jinxes.json).</summary>
public sealed record OfficialJinxes
{
    public required string Id { get; init; }

    public IReadOnlyList<OfficialJinx> Jinx { get; init; } = [];
}
