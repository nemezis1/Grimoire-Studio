namespace GrimoireStudio.Data;

/// <summary>
/// The published waking order (botc-release, resources/data/nightsheet.json): role ids, plus steps such as "dusk" or
/// "minioninfo" that are not roles.
/// </summary>
public sealed record OfficialNightSheet
{
    public IReadOnlyList<string> FirstNight { get; init; } = [];

    public IReadOnlyList<string> OtherNight { get; init; } = [];
}
