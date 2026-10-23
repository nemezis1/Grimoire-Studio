using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrimoireStudio.Data;

/// <summary>
/// The JSON format of the official Script Tool (script.bloodontheclocktower.com): an array with an optional
/// <c>{"id": "_meta", "name": ...}</c> entry followed by role ids (strings or <c>{"id": ...}</c>) and full objects for
/// homebrew characters. Homebrew texts are English only, so imported roles use them for Polish too.
/// </summary>
public static class ScriptToolFormat
{
    private const string MetaId = "_meta";

    /// <summary>Script Tool ids may contain underscores or dashes (fortune_teller); ours are letters and digits only.</summary>
    public static string NormalizeId(string id) => new(id.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    /// <exception cref="FormatException">The text is not a Script Tool JSON.</exception>
    public static ScriptToolImport Import(string json, GameData data, string scriptId)
    {
        JsonArray items;
        try
        {
            items = JsonNode.Parse(json) as JsonArray ?? throw new FormatException("Plik nie jest listą ról ze Script Tool.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"Niepoprawny JSON: {e.Message}");
        }

        var name = "Importowany skrypt";
        var roleIds = new List<string>();
        var newRoles = new List<Role>();
        var newTeams = new List<TeamInfo>();
        var newJinxes = new List<JinxOverride>();
        var unknown = new List<string>();

        foreach (var item in items)
        {
            var rawId = item switch
            {
                JsonValue value when value.TryGetValue<string>(out var text) => text,
                JsonObject obj => Text(obj, "id"),
                _ => string.Empty,
            };

            if (rawId == MetaId && item is JsonObject meta)
            {
                name = Text(meta, "name") is { Length: > 0 } metaName ? metaName : name;
                continue;
            }

            var id = NormalizeId(rawId);
            if (id.Length == 0 || roleIds.Contains(id))
            {
                continue;
            }

            if (data.FindRole(id) is null && newRoles.All(r => r.Id != id))
            {
                if (item is JsonObject homebrew && homebrew.ContainsKey("team"))
                {
                    var role = ReadRole(homebrew, id, data, newTeams);
                    newRoles.Add(role);
                    newJinxes.AddRange(ReadJinxes(homebrew, id));
                }
                else
                {
                    unknown.Add(rawId);
                    continue;
                }
            }

            roleIds.Add(id);
        }

        var script = new Script { Id = scriptId, Name = name, Roles = roleIds, CustomIcon = "custom" };
        return new ScriptToolImport(script, newRoles, newTeams, newJinxes, unknown);
    }

    /// <summary>
    /// Script Tool JSON of a script. Roles from the data folder are written as ids; roles that exist only in this
    /// browser (not in <paramref name="original"/>) are written in full, with their jinxes.
    /// </summary>
    public static string Export(Script script, GameData data, GameData original)
    {
        var items = new JsonArray { new JsonObject { ["id"] = MetaId, ["name"] = script.Name, ["author"] = string.Empty } };

        foreach (var role in data.RolesOf(script))
        {
            items.Add(original.FindRole(role.Id) is null ? WriteRole(role, data) : JsonValue.Create(role.Id));
        }

        return items.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static Role ReadRole(JsonObject obj, string id, GameData data, List<TeamInfo> newTeams)
    {
        var name = Text(obj, "name") is { Length: > 0 } text ? text : id;
        var ability = Text(obj, "ability");
        var reminders = Texts(obj, "reminders");
        var globalReminders = Texts(obj, "remindersGlobal");

        return new Role
        {
            Id = id,
            NameEn = name,
            NamePl = name,
            Team = ReadTeam(Text(obj, "team"), data, newTeams),
            AbilityEn = ability,
            AbilityPl = ability,
            FirstNight = Number(obj, "firstNight"),
            OtherNight = Number(obj, "otherNight"),
            FirstNightReminderEn = Text(obj, "firstNightReminder"),
            FirstNightReminderPl = Text(obj, "firstNightReminder"),
            OtherNightReminderEn = Text(obj, "otherNightReminder"),
            OtherNightReminderPl = Text(obj, "otherNightReminder"),
            RemindersEn = reminders,
            RemindersPl = reminders,
            RemindersGlobalEn = globalReminders,
            RemindersGlobalPl = globalReminders,
            Setup = obj["setup"] is JsonValue setup && setup.TryGetValue<bool>(out var value) && value,
        };
    }

    /// <summary>Known team, or a new team created from the id (e.g. "loric") so the role keeps its type.</summary>
    private static string ReadTeam(string team, GameData data, List<TeamInfo> newTeams)
    {
        var id = NormalizeId(team) switch
        {
            "traveller" => TeamIds.Traveler,
            "" => TeamIds.Townsfolk,
            var other => other,
        };

        if (data.FindTeam(id) is null && newTeams.All(t => t.Id != id))
        {
            var label = char.ToUpperInvariant(id[0]) + id[1..];
            newTeams.Add(new TeamInfo
            {
                Id = id,
                NamePl = label,
                NameEn = label,
                PluralPl = label,
                PluralEn = label,
                Order = data.Teams.Concat(newTeams).Max(t => t.Order) + 10,
                Extra = true,
            });
        }

        return id;
    }

    private static IEnumerable<JinxOverride> ReadJinxes(JsonObject obj, string ownerId) =>
        (obj["jinxes"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(j => (Id: NormalizeId(Text(j, "id")), Reason: Text(j, "reason")))
            .Where(j => j.Id.Length > 0)
            .Select(j => new JinxOverride { OwnerId = ownerId, Jinx = new Jinx { Id = j.Id, ReasonEn = j.Reason, ReasonPl = j.Reason } });

    private static JsonObject WriteRole(Role role, GameData data)
    {
        var obj = new JsonObject
        {
            ["id"] = role.Id,
            ["name"] = role.NameEn,
            ["team"] = role.Team == TeamIds.Traveler ? "traveller" : role.Team,
            ["ability"] = role.AbilityEn,
            ["firstNight"] = role.FirstNight,
            ["otherNight"] = role.OtherNight,
            ["firstNightReminder"] = role.FirstNightReminderEn,
            ["otherNightReminder"] = role.OtherNightReminderEn,
            ["reminders"] = new JsonArray(role.RemindersEn.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()),
            ["remindersGlobal"] = new JsonArray(role.RemindersGlobalEn.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()),
            ["setup"] = role.Setup,
        };

        var jinxes = data.Jinxes
            .Where(o => string.Equals(o.Id, role.Id, StringComparison.OrdinalIgnoreCase))
            .SelectMany(o => o.Jinxes)
            .Select(j => (JsonNode?)new JsonObject { ["id"] = j.Id, ["reason"] = j.ReasonEn })
            .ToArray();
        if (jinxes.Length > 0)
        {
            obj["jinxes"] = new JsonArray(jinxes);
        }

        return obj;
    }

    private static string Text(JsonObject obj, string property) =>
        obj[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty;

    private static IReadOnlyList<string> Texts(JsonObject obj, string property) =>
        (obj[property] as JsonArray ?? [])
            .Select(n => n is JsonValue v && v.TryGetValue<string>(out var text) ? text : null)
            .OfType<string>()
            .ToList();

    private static int Number(JsonObject obj, string property) =>
        obj[property] is JsonValue value && value.TryGetValue<double>(out var number) ? (int)Math.Round(number) : 0;
}
