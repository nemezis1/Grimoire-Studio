namespace GrimoireStudio.Data;

/// <summary>
/// A waking order of the wiki (first night or the other nights) that differs from the published one. Roles that are
/// not official keep their place after the role they follow now.
/// </summary>
public sealed record OfficialNightUpdate
{
    public required bool FirstNight { get; init; }

    /// <summary>Role ids in the wiki's waking order now.</summary>
    public required IReadOnlyList<string> Old { get; init; }

    /// <summary>Role ids in the published waking order (with the wiki's unofficial roles in their places).</summary>
    public required IReadOnlyList<string> New { get; init; }

    public string Key => FirstNight ? "night:first" : "night:other";

    /// <summary>Roles that start waking this night.</summary>
    public IEnumerable<string> Added => New.Except(Old);

    /// <summary>Roles that stop waking this night.</summary>
    public IEnumerable<string> Removed => Old.Except(New);
}
