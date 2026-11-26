namespace GrimoireStudio.Data;

/// <summary>How the published data differs from the wiki's (<see cref="OfficialUpdates.Compare"/>).</summary>
public sealed record OfficialComparison
{
    public IReadOnlyList<OfficialRoleUpdate> Roles { get; init; } = [];

    public IReadOnlyList<OfficialJinxUpdate> Jinxes { get; init; } = [];

    public IReadOnlyList<OfficialNightUpdate> Nights { get; init; } = [];

    public int Count => Roles.Sum(r => r.Fields.Count) + Jinxes.Count + Nights.Count;
}
