using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// A search result: what to show and where it leads. <see cref="Key"/> names it in the recently opened list, so it can
/// be looked up again in fresh data (see <see cref="Resolve"/>).
/// </summary>
public sealed record SearchHit
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public required string Href { get; init; }

    /// <summary>Team id giving the hit its colour.</summary>
    public string? Team { get; init; }

    /// <summary>Role whose icon is shown.</summary>
    public Role? Role { get; init; }

    /// <summary>Script whose picture is shown.</summary>
    public Script? Script { get; init; }

    public static SearchHit For(Role role) => new()
    {
        Key = $"role:{role.Id}",
        Title = role.NamePl,
        Subtitle = role.NameEn == role.NamePl ? null : role.NameEn,
        Href = $"roles/{role.Id}",
        Team = role.Team,
        Role = role,
    };

    public static SearchHit For(Script script) => new()
    {
        Key = $"script:{script.Id}",
        Title = script.Name,
        Subtitle = RoleCount(script.Roles.Count),
        Href = $"scripts/{script.Id}",
        Script = script,
    };

    public static SearchHit For(Role owner, Role other, Jinx jinx) => new()
    {
        Key = $"jinx:{owner.Id}:{other.Id}",
        Title = $"{owner.NamePl} × {other.NamePl}",
        Subtitle = jinx.ReasonPl,
        Href = $"jinxes?q={Uri.EscapeDataString(owner.NamePl.Trim('«', '»', ' '))}",
        Team = owner.Team,
        Role = owner,
    };

    public static SearchHit For(TeamInfo team) => new()
    {
        Key = $"team:{team.Id}",
        Title = team.PluralPl,
        Subtitle = team.PluralEn,
        Href = $"roles?team={team.Id}",
        Team = team.Id,
    };

    private static string RoleCount(int n) => n switch
    {
        1 => "1 rola",
        _ when n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 => $"{n} role",
        _ => $"{n} ról",
    };

    /// <summary>The hit a <see cref="Key"/> names in <paramref name="data"/>; null when the item is gone.</summary>
    public static SearchHit? Resolve(GameData data, string key)
    {
        var parts = key.Split(':');
        return parts switch
        {
            ["role", var id] => data.FindRole(id) is { } role ? For(role) : null,
            ["script", var id] => data.FindScript(id) is { } script ? For(script) : null,
            ["team", var id] => data.FindTeam(id) is { } team ? For(team) : null,
            ["jinx", var ownerId, var otherId] =>
                data.FindRole(ownerId) is { } owner && data.FindRole(otherId) is { } other
                && data.Jinxes.FirstOrDefault(o => o.Id == owner.Id)?.Jinxes.FirstOrDefault(j => j.Id == other.Id) is { } jinx
                    ? For(owner, other, jinx)
                    : null,
            _ => null,
        };
    }
}
