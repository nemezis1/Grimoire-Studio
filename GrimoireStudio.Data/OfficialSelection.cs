namespace GrimoireStudio.Data;

/// <summary>
/// What the admin takes from an <see cref="OfficialComparison"/>: ticked role fields with their Polish texts, ticked
/// jinxes with theirs, ticked night orders, and icons for new roles.
/// </summary>
public sealed class OfficialSelection
{
    /// <summary>Ticked fields by role id.</summary>
    public Dictionary<string, HashSet<string>> RoleFields { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Polish texts by role id and field key; a list field holds one item a line.</summary>
    public Dictionary<string, Dictionary<string, string>> RolePolish { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Icons (data URLs) for new roles, by role id.</summary>
    public Dictionary<string, string> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticked jinxes (<see cref="OfficialJinxUpdate.Key"/>) with their Polish reasons.</summary>
    public Dictionary<string, string> Jinxes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticked night orders (<see cref="OfficialNightUpdate.Key"/>).</summary>
    public HashSet<string> Nights { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsTicked(string roleId, string field) => RoleFields.TryGetValue(roleId, out var fields) && fields.Contains(field);

    public string? PolishFor(string roleId, string field) =>
        RolePolish.TryGetValue(roleId, out var texts) && texts.TryGetValue(field, out var text) ? text : null;

    public int Count => RoleFields.Values.Sum(f => f.Count) + Jinxes.Count + Nights.Count;
}
