using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrimoireStudio.Data;

/// <summary>Everything stored in the data folder.</summary>
public sealed record GameData
{
    public const string RolesFile = "roles.json";
    public const string FabledFile = "fabled.json";
    public const string ScriptsFile = "scripts.json";
    public const string JinxesFile = "hatred.json";
    public const string TeamsFile = "teams.json";

    /// <summary>Role icons, <c>icons/&lt;role id&gt;.png</c>.</summary>
    public const string IconsFolder = "icons";

    /// <summary>Script pictures, <c>editions/&lt;icon name&gt;.png</c> (see <see cref="Script.IconName"/>).</summary>
    public const string EditionsFolder = "editions";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public IReadOnlyList<Role> Roles { get; init; } = [];
    public IReadOnlyList<Role> Fabled { get; init; } = [];
    public ScriptCatalog Scripts { get; init; } = new();
    public IReadOnlyList<RoleJinxes> Jinxes { get; init; } = [];

    /// <summary>Teams (character types) in list order.</summary>
    public IReadOnlyList<TeamInfo> Teams { get; init; } = [];

    public IEnumerable<Role> AllRoles => Roles.Concat(Fabled);

    public static GameData Load(string directory) => new()
    {
        Roles = Read<List<Role>>(directory, RolesFile),
        Fabled = Read<List<Role>>(directory, FabledFile),
        Scripts = Read<ScriptCatalog>(directory, ScriptsFile),
        Jinxes = Read<List<RoleJinxes>>(directory, JinxesFile),
        Teams = Read<List<TeamInfo>>(directory, TeamsFile),
    };

    private static T Read<T>(string directory, string file)
    {
        var path = Path.Combine(directory, file);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidDataException($"{file} is empty.");
    }
}
