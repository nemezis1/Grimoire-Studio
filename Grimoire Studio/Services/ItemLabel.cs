using System.Text.Json;
using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Names of roles, scripts, teams and jinxes for lists of changes, by kind and id.</summary>
public static class ItemLabel
{
    /// <summary>The item's name in <paramref name="data"/>, else the name in <paramref name="json"/>, else its id.</summary>
    public static string Of(GameData data, string kind, string id, JsonElement? json = null) => kind switch
    {
        "role" => data.FindRole(id)?.NamePl ?? Text(json, "namePl") ?? id,
        "script" => data.FindScript(id)?.Name ?? Text(json, "name") ?? id,
        "team" => data.FindTeam(id)?.PluralPl ?? Text(json, "pluralPl") ?? id,
        "jinx" => string.Join(" + ", id.Split('/').Select(role => data.FindRole(role)?.NamePl ?? role)),
        _ => id,
    };

    /// <summary>Kind and id of a change set key (<see cref="LocalBackup.Keys"/>).</summary>
    public static (string Kind, string Id) Split(string key) =>
        key.IndexOf(':') is var colon and >= 0 ? (key[..colon], key[(colon + 1)..]) : (key, string.Empty);

    private static string? Text(JsonElement? json, string field) =>
        json is { ValueKind: JsonValueKind.Object } obj
            && obj.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
