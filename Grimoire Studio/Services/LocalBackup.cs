using System.Text.Json.Serialization;
using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>File format of an export of all local changes.</summary>
public sealed record LocalBackup
{
    public const string FormatName = "grimoire-studio-local";

    public string Format { get; init; } = FormatName;
    public int Version { get; init; } = 1;
    public DateTimeOffset ExportedAt { get; init; }

    public IReadOnlyList<LocalRole> Roles { get; init; } = [];
    public IReadOnlyList<Script> Scripts { get; init; } = [];
    public IReadOnlyList<TeamInfo> Teams { get; init; } = [];
    public IReadOnlyList<JinxOverride> Jinxes { get; init; } = [];

    [JsonIgnore]
    public int Count => Roles.Count + Scripts.Count + Teams.Count + Jinxes.Count;

    public static string RoleKey(string id) => $"role:{id}";
    public static string ScriptKey(string id) => $"script:{id}";
    public static string TeamKey(string id) => $"team:{id}";
    public static string JinxKey(JinxOverride jinx) => $"jinx:{jinx.Key}";

    /// <summary>Keys of all items, for selecting some of them (<see cref="Only"/>).</summary>
    public IEnumerable<string> Keys() =>
        Roles.Select(r => RoleKey(r.Role.Id))
            .Concat(Scripts.Select(s => ScriptKey(s.Id)))
            .Concat(Teams.Select(t => TeamKey(t.Id)))
            .Concat(Jinxes.Select(JinxKey));

    /// <summary>The items whose keys are in <paramref name="keys"/>.</summary>
    public LocalBackup Only(IReadOnlySet<string> keys) => this with
    {
        Roles = Roles.Where(r => keys.Contains(RoleKey(r.Role.Id))).ToList(),
        Scripts = Scripts.Where(s => keys.Contains(ScriptKey(s.Id))).ToList(),
        Teams = Teams.Where(t => keys.Contains(TeamKey(t.Id))).ToList(),
        Jinxes = Jinxes.Where(j => keys.Contains(JinxKey(j))).ToList(),
    };
}
