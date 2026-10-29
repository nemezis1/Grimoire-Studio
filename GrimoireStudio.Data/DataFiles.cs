using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace GrimoireStudio.Data;

/// <summary>
/// Writes <see cref="GameData"/> as the JSON files of the data folder, always in the same form: fields in declaration
/// order, two-space indent, lists of plain values on one line, LF line ends and a final newline. Writing unchanged data
/// gives byte-identical files, so a change to one role is a few changed lines in git.
/// </summary>
public static class DataFiles
{
    private static readonly JsonSerializerOptions Options = new(GameData.JsonOptions)
    {
        // Polish letters and typographic quotes stay readable instead of \uXXXX.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitOptionalFields } },
    };

    /// <summary>Contents of the JSON files by file name (<see cref="GameData.RolesFile"/> etc.).</summary>
    public static IReadOnlyDictionary<string, string> Serialize(GameData data) => new Dictionary<string, string>
    {
        [GameData.RolesFile] = ToJson(data.Roles),
        [GameData.FabledFile] = ToJson(data.Fabled),
        [GameData.ScriptsFile] = ToJson(data.Scripts),
        [GameData.JinxesFile] = ToJson(data.Jinxes),
        [GameData.TeamsFile] = ToJson(data.Teams),
    };

    public static string ToJson<T>(T value)
    {
        using var document = JsonSerializer.SerializeToDocument(value, Options);
        var text = new StringBuilder();
        Write(document.RootElement, text, 0);
        return text.Append('\n').ToString();
    }

    /// <summary>
    /// Fields left out when they hold nothing, as the data files always did: global reminders (rare), script flags,
    /// and uploaded pictures (those become files, see <see cref="RepositoryExport"/>).
    /// </summary>
    private static void OmitOptionalFields(JsonTypeInfo type)
    {
        foreach (var property in type.Properties)
        {
            property.ShouldSerialize = (property.Name, IsRole: type.Type == typeof(Role), IsScript: type.Type == typeof(Script)) switch
            {
                ("remindersGlobalEn" or "remindersGlobalPl", true, _) => (_, value) => value is IReadOnlyCollection<string> { Count: > 0 },
                ("isOfficial", _, true) => (_, value) => value is true,
                ("customIcon", _, true) => (_, value) => value is string { Length: > 0 },
                ("image", _, true) => (_, _) => false,
                _ => property.ShouldSerialize,
            };
        }
    }

    private static void Write(JsonElement element, StringBuilder text, int depth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteItems(
                    text,
                    depth,
                    '{',
                    '}',
                    element.EnumerateObject().Select<JsonProperty, Action>(p => () =>
                    {
                        text.Append(JsonSerializer.Serialize(p.Name, Options)).Append(": ");
                        Write(p.Value, text, depth + 1);
                    }).ToList());
                break;

            case JsonValueKind.Array when element.EnumerateArray().All(IsPlain):
                var values = element.EnumerateArray().Select(v => v.GetRawText()).ToList();
                text.Append(values.Count == 0 ? "[]" : $"[ {string.Join(", ", values)} ]");
                break;

            case JsonValueKind.Array:
                WriteItems(
                    text,
                    depth,
                    '[',
                    ']',
                    element.EnumerateArray().Select<JsonElement, Action>(v => () => Write(v, text, depth + 1)).ToList());
                break;

            default:
                text.Append(element.GetRawText());
                break;
        }
    }

    private static void WriteItems(StringBuilder text, int depth, char open, char close, IReadOnlyList<Action> items)
    {
        if (items.Count == 0)
        {
            text.Append(open).Append(close);
            return;
        }

        text.Append(open).Append('\n');
        for (var i = 0; i < items.Count; i++)
        {
            text.Append(' ', (depth + 1) * 2);
            items[i]();
            text.Append(i < items.Count - 1 ? ",\n" : "\n");
        }

        text.Append(' ', depth * 2).Append(close);
    }

    private static bool IsPlain(JsonElement element) => element.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array);
}
