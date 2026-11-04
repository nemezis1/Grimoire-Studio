namespace GrimoireStudio.Data;

/// <summary>
/// What a script offers for play: how many roles of each team it has against the standard distribution
/// (<see cref="PlayerCount.Table"/>), its jinxes, roles that need another role on the script, and setup or night
/// concerns. The editor shows it while roles are picked.
/// </summary>
public sealed record ScriptAnalysis
{
    /// <summary>Good roles not in play the demon needs to bluff with.</summary>
    public const int DemonBluffs = 3;

    /// <summary>The fabled that brings the jinxes into play; a script with jinxes should list it.</summary>
    public const string JinxFabled = "djinn";

    /// <summary>A role that only works with another role on the script, e.g. the Huntsman with the Damsel.</summary>
    public static IReadOnlyDictionary<string, string> Requires { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["huntsman"] = "damsel",
        ["choirboy"] = "king",
    };

    /// <summary>The counted teams, in table order, with how many roles of each the script has.</summary>
    public required IReadOnlyList<TeamCoverage> Teams { get; init; }

    /// <summary>
    /// Smallest player count (5 to 15) the script cannot fill: too few roles of a team, or too few good roles left for
    /// the demon's bluffs. Null when it covers every count.
    /// </summary>
    public int? ShortFrom { get; init; }

    /// <summary>Smallest player count at which the good roles left out of play are fewer than <see cref="DemonBluffs"/>.</summary>
    public int? BluffsShortFrom { get; init; }

    /// <summary>Jinxes between two roles of the script.</summary>
    public IReadOnlyList<(Role Owner, Role Other, Jinx Jinx)> Jinxes { get; init; } = [];

    /// <summary>Roles whose partner (<see cref="Requires"/>, or the jinx fabled) is missing from the script.</summary>
    public IReadOnlyList<(Role Role, Role Needed)> Missing { get; init; } = [];

    /// <summary>Roles that change the game setup (e.g. the Baron's extra outsiders).</summary>
    public IReadOnlyList<Role> SetupRoles { get; init; } = [];

    /// <summary>Townsfolk that wake on the first night, i.e. good players' first information.</summary>
    public int FirstNightTownsfolk { get; init; }

    public int FirstNightWakers { get; init; }

    public int OtherNightWakers { get; init; }

    /// <summary>The script cannot be played at all: it has no demon, no minion or no townsfolk.</summary>
    public bool IsUnplayable => Teams.Any(t => t.Count == 0 && t.Team != TeamIds.Outsider);

    /// <summary>Setup roles can add outsiders (up to two more), so a script with them wants a few spare outsiders.</summary>
    public bool FewOutsidersForSetup =>
        SetupRoles.Count > 0 && Count(TeamIds.Outsider) < Needed(TeamIds.Outsider) + 2;

    public int Count(string team) => Teams.FirstOrDefault(t => t.Team == team)?.Count ?? 0;

    private int Needed(string team) => Teams.FirstOrDefault(t => t.Team == team)?.Needed ?? 0;

    public static ScriptAnalysis Analyze(GameData data, IReadOnlyCollection<Role> roles)
    {
        var ids = roles.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int CountOf(string team) => roles.Count(r => r.Team == team);

        var teams = new (string Team, Func<PlayerCount, int> Need)[]
        {
            (TeamIds.Townsfolk, p => p.Townsfolk),
            (TeamIds.Outsider, p => p.Outsiders),
            (TeamIds.Minion, p => p.Minions),
            (TeamIds.Demon, p => p.Demons),
        };

        var coverage = teams
            .Select(t => new TeamCoverage(
                t.Team,
                CountOf(t.Team),
                PlayerCount.Table.Max(t.Need),
                PlayerCount.Table.FirstOrDefault(p => t.Need(p) > CountOf(t.Team))?.Players))
            .ToList();

        var good = CountOf(TeamIds.Townsfolk) + CountOf(TeamIds.Outsider);
        var bluffsShort = PlayerCount.Table.FirstOrDefault(p => good - p.Townsfolk - p.Outsiders < DemonBluffs)?.Players;
        var shortFrom = coverage.Select(t => t.ShortFrom).Append(bluffsShort).Where(p => p is not null).Min();

        var jinxes = data.Jinxes
            .Where(o => ids.Contains(o.Id))
            .SelectMany(o => o.Jinxes.Where(j => ids.Contains(j.Id)).Select(j => (Owner: data.FindRole(o.Id), Other: data.FindRole(j.Id), Jinx: j)))
            .Where(x => x.Owner is not null && x.Other is not null)
            .Select(x => (x.Owner!, x.Other!, x.Jinx))
            .ToList();

        var missing = roles
            .Select(r => (Role: r, Needed: Requires.TryGetValue(r.Id, out var needed) && !ids.Contains(needed) ? data.FindRole(needed) : null))
            .Where(x => x.Needed is not null)
            .Select(x => (x.Role, x.Needed!))
            .ToList();

        if (jinxes.Count > 0 && !ids.Contains(JinxFabled) && data.FindRole(JinxFabled) is { } djinn)
        {
            // The jinxes are the reason, so the first jinxed role stands for them.
            missing.Add((jinxes[0].Item1, djinn));
        }

        return new ScriptAnalysis
        {
            Teams = coverage,
            ShortFrom = shortFrom,
            BluffsShortFrom = bluffsShort,
            Jinxes = jinxes,
            Missing = missing,
            SetupRoles = roles.Where(r => r.Setup).ToList(),
            FirstNightTownsfolk = roles.Count(r => r.Team == TeamIds.Townsfolk && r.WakesFirstNight),
            FirstNightWakers = roles.Count(r => r.WakesFirstNight),
            OtherNightWakers = roles.Count(r => r.WakesOtherNights),
        };
    }
}
