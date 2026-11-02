using System.Text.Json;

namespace Grimoire_Studio.Services;

/// <summary>Field-by-field differences of an item (role, script, jinx) between two versions of the data files.</summary>
public static class ItemDiff
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["namePl"] = "Nazwa (PL)",
        ["nameEn"] = "Nazwa (EN)",
        ["name"] = "Nazwa",
        ["team"] = "Drużyna",
        ["edition"] = "Edycja",
        ["abilityPl"] = "Zdolność (PL)",
        ["abilityEn"] = "Zdolność (EN)",
        ["firstNight"] = "Pierwsza noc – kolejność",
        ["firstNightReminderPl"] = "Pierwsza noc – opis (PL)",
        ["firstNightReminderEn"] = "Pierwsza noc – opis (EN)",
        ["otherNight"] = "Pozostałe noce – kolejność",
        ["otherNightReminderPl"] = "Pozostałe noce – opis (PL)",
        ["otherNightReminderEn"] = "Pozostałe noce – opis (EN)",
        ["remindersPl"] = "Znaczniki (PL)",
        ["remindersEn"] = "Znaczniki (EN)",
        ["remindersGlobalPl"] = "Znaczniki globalne (PL)",
        ["remindersGlobalEn"] = "Znaczniki globalne (EN)",
        ["setup"] = "Zmienia przygotowanie gry",
        ["roles"] = "Role",
        ["isOfficial"] = "Oficjalny",
        ["customIcon"] = "Ikona edycji",
        ["reasonPl"] = "Treść (PL)",
        ["reasonEn"] = "Treść (EN)",
        ["owner"] = "Zapisany przy roli",
    };

    /// <summary>Changed fields in a readable order; a list of roles shows what was added and removed.</summary>
    public static IReadOnlyList<(string Label, string Before, string After)> Changes(JsonElement? before, JsonElement? after)
    {
        var old = Fields(before);
        var now = Fields(after);
        var keys = Labels.Keys.Where(k => old.ContainsKey(k) || now.ContainsKey(k))
            .Concat(old.Keys.Concat(now.Keys).Where(k => !Labels.ContainsKey(k) && k != "id").Distinct());

        var rows = new List<(string, string, string)>();
        foreach (var key in keys)
        {
            old.TryGetValue(key, out var a);
            now.TryGetValue(key, out var b);
            if (a.ValueKind == b.ValueKind && a.ValueKind != JsonValueKind.Undefined && a.GetRawText() == b.GetRawText())
            {
                continue;
            }

            if (key == "roles" && a.ValueKind == JsonValueKind.Array && b.ValueKind == JsonValueKind.Array)
            {
                var was = Strings(a).ToHashSet();
                var isNow = Strings(b).ToHashSet();
                rows.Add((Labels[key],
                    "usunięte: " + Join(was.Except(isNow)),
                    "dodane: " + Join(isNow.Except(was))));
                continue;
            }

            rows.Add((Labels.GetValueOrDefault(key, key), Text(a), Text(b)));
        }

        return rows;
    }

    private static Dictionary<string, JsonElement> Fields(JsonElement? item) =>
        item is { ValueKind: JsonValueKind.Object } obj
            ? obj.EnumerateObject().ToDictionary(p => p.Name, p => p.Value)
            : [];

    private static IEnumerable<string> Strings(JsonElement array) => array.EnumerateArray().Select(Text);

    private static string Join(IEnumerable<string> values) => values.Any() ? string.Join(", ", values) : "–";

    private static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => "–",
        JsonValueKind.String => value.GetString() is { Length: > 0 } s ? s : "–",
        JsonValueKind.True => "tak",
        JsonValueKind.False => "nie",
        JsonValueKind.Array => Join(Strings(value)),
        _ => value.GetRawText(),
    };
}
