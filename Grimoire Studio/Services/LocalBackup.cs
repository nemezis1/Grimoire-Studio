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
}
