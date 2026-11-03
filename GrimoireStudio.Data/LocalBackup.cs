using System.Text.Json.Serialization;

namespace GrimoireStudio.Data;

/// <summary>
/// A set of changes over the data folder (a browser's change set, a proposal, or its export file). Applied with
/// <see cref="ApplyTo"/>; written to the data folder with <see cref="RepositoryExport"/>.
/// </summary>
public sealed record LocalBackup
{
    public const string FormatName = "grimoire-studio-local";

    public string Format { get; init; } = FormatName;
    public int Version { get; init; } = 1;
    public DateTimeOffset ExportedAt { get; init; }

    /// <summary>
    /// <see cref="GameData.Version"/> of the data the changes were made on (the oldest, when made over several
    /// versions); empty when unknown (exports from before versions were recorded).
    /// </summary>
    public string BaseVersion { get; init; } = string.Empty;

    /// <summary>
    /// True when the changes were made on another data version than <paramref name="current"/>; a short commit id and
    /// the full one name the same version.
    /// </summary>
    public bool IsOutdated(string current) =>
        BaseVersion.Length > 0 && current.Length > 0
        && !BaseVersion.StartsWith(current, StringComparison.OrdinalIgnoreCase)
        && !current.StartsWith(BaseVersion, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<LocalRole> Roles { get; init; } = [];
    public IReadOnlyList<Script> Scripts { get; init; } = [];
    public IReadOnlyList<TeamInfo> Teams { get; init; } = [];
    public IReadOnlyList<JinxOverride> Jinxes { get; init; } = [];

    /// <summary>
    /// Keys (<see cref="RoleKey"/>, <see cref="ScriptKey"/>, <see cref="TeamKey"/>) of roles, scripts and teams
    /// removed; jinxes are removed with <see cref="JinxOverride.Removed"/>.
    /// </summary>
    public IReadOnlyList<string> Removed { get; init; } = [];

    [JsonIgnore]
    public int Count => Roles.Count + Scripts.Count + Teams.Count + Jinxes.Count + Removed.Count;

    public static string RoleKey(string id) => $"role:{id}";
    public static string ScriptKey(string id) => $"script:{id}";
    public static string TeamKey(string id) => $"team:{id}";
    public static string JinxKey(JinxOverride jinx) => $"jinx:{jinx.Key}";

    /// <summary>Keys of all items, for selecting some of them (<see cref="Only"/>).</summary>
    public IEnumerable<string> Keys() =>
        Roles.Select(r => RoleKey(r.Role.Id))
            .Concat(Scripts.Select(s => ScriptKey(s.Id)))
            .Concat(Teams.Select(t => TeamKey(t.Id)))
            .Concat(Jinxes.Select(JinxKey))
            .Concat(Removed);

    /// <summary>Data with these changes applied on top of <paramref name="data"/>.</summary>
    public GameData ApplyTo(GameData data) =>
        data
            .WithTeams(Teams)
            .WithRoles(Roles.Select(r => r.Role))
            .WithScripts(Scripts)
            .WithJinxes(Jinxes)
            .Without(Removed);

    /// <summary>
    /// These items as they are in <paramref name="data"/>, for comparing with later (removed ones too); items missing
    /// there (new ones) are left out.
    /// </summary>
    public LocalBackup As(GameData data) => this with
    {
        Roles = Roles.Select(r => r.Role.Id).Concat(RemovedIds("role")).Select(data.FindRole).OfType<Role>()
            .Select(r => new LocalRole(r, null)).ToList(),
        Scripts = Scripts.Select(s => s.Id).Concat(RemovedIds("script")).Select(data.FindScript).OfType<Script>().ToList(),
        Teams = Teams.Select(t => t.Id).Concat(RemovedIds("team")).Select(data.FindTeam).OfType<TeamInfo>().ToList(),
        Removed = [],
        Jinxes = Jinxes
            .Select(j => (j.OwnerId, Jinx: data.Jinxes
                .Where(o => string.Equals(o.Id, j.OwnerId, StringComparison.OrdinalIgnoreCase))
                .SelectMany(o => o.Jinxes)
                .FirstOrDefault(x => string.Equals(x.Id, j.Jinx.Id, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Jinx is not null)
            .Select(x => new JinxOverride { OwnerId = x.OwnerId, Jinx = x.Jinx! })
            .ToList(),
    };

    /// <summary>The items whose keys are in <paramref name="keys"/>.</summary>
    public LocalBackup Only(IReadOnlySet<string> keys) => this with
    {
        Roles = Roles.Where(r => keys.Contains(RoleKey(r.Role.Id))).ToList(),
        Scripts = Scripts.Where(s => keys.Contains(ScriptKey(s.Id))).ToList(),
        Teams = Teams.Where(t => keys.Contains(TeamKey(t.Id))).ToList(),
        Jinxes = Jinxes.Where(j => keys.Contains(JinxKey(j))).ToList(),
        Removed = Removed.Where(keys.Contains).ToList(),
    };

    /// <summary>Ids of the removed items of one kind ("role", "script", "team").</summary>
    public IEnumerable<string> RemovedIds(string kind) =>
        Removed.Where(k => k.StartsWith(kind + ":", StringComparison.Ordinal)).Select(k => k[(kind.Length + 1)..]);
}
