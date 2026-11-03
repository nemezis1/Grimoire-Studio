namespace GrimoireStudio.Data;

/// <summary>
/// Items of a change set that were changed in the data folder too since the changes were made on it, so sending the
/// changes would silently overwrite someone else's work.
/// </summary>
public static class ItemConflicts
{
    /// <summary>
    /// Keys (<see cref="LocalBackup.Keys"/>) of the items <paramref name="changes"/> touches that differ between
    /// <paramref name="before"/> (the data the changes were made on) and <paramref name="now"/>, unless the changes
    /// make them just as they are now.
    /// </summary>
    public static IReadOnlyList<string> Find(GameData before, GameData now, LocalBackup changes)
    {
        var mine = changes.ApplyTo(before);
        return changes.Keys()
            .Where(key => Json(before, key) != Json(now, key) && Json(now, key) != Json(mine, key))
            .ToList();
    }

    /// <summary>
    /// One item, by its key (<c>role:chef</c>, <c>jinx:imp/chef</c>…), as it is written to the data folder; null when
    /// <paramref name="data"/> has no such item.
    /// </summary>
    public static string? Json(GameData data, string key)
    {
        var colon = key.IndexOf(':');
        var (kind, id) = colon < 0 ? (key, string.Empty) : (key[..colon], key[(colon + 1)..]);
        return kind switch
        {
            "role" => data.FindRole(id) is { } role ? DataFiles.ToJson(role) : null,
            "script" => data.FindScript(id) is { } script ? DataFiles.ToJson(script) : null,
            "team" => data.FindTeam(id) is { } team ? DataFiles.ToJson(team) : null,
            "jinx" => FindJinx(data, id) is { } jinx ? DataFiles.ToJson(jinx) : null,
            _ => null,
        };
    }

    /// <summary>The jinx <c>owner/other</c>, stored under the owner.</summary>
    private static Jinx? FindJinx(GameData data, string id)
    {
        var slash = id.IndexOf('/');
        if (slash < 0)
        {
            return null;
        }

        var (owner, other) = (id[..slash], id[(slash + 1)..]);
        return data.Jinxes
            .Where(o => string.Equals(o.Id, owner, StringComparison.OrdinalIgnoreCase))
            .SelectMany(o => o.Jinxes)
            .FirstOrDefault(j => string.Equals(j.Id, other, StringComparison.OrdinalIgnoreCase));
    }
}
