using System.Text.Json;

namespace GrimoireStudio.Data;

/// <summary>The data The Pandemonium Institute publishes in its botc-release repository.</summary>
public sealed record OfficialRelease
{
    /// <summary>Repository with the published data, on GitHub.</summary>
    public const string Repository = "ThePandemoniumInstitute/botc-release";

    /// <summary>Folder of the data files in <see cref="Repository"/>.</summary>
    public const string DataPath = "resources/data";

    public const string RolesFile = "roles.json";
    public const string JinxesFile = "jinxes.json";
    public const string NightSheetFile = "nightsheet.json";

    /// <summary>Raw address of one data file at a branch or commit.</summary>
    public static string FileUrl(string file, string reference = "main") =>
        $"https://raw.githubusercontent.com/{Repository}/{reference}/{DataPath}/{file}";

    /// <summary>
    /// Addresses where the published icon of a role may be, best first: <c>resources/characters/&lt;edition&gt;/</c>
    /// holds <c>&lt;id&gt;_g.webp</c> (good), <c>&lt;id&gt;_e.webp</c> (evil) and, for travellers and fabled,
    /// <c>&lt;id&gt;.webp</c>. Minions and demons look evil, travellers, fabled and loric as they are, the others good.
    /// </summary>
    public static IReadOnlyList<string> IconUrls(OfficialRole role, string reference = "main")
    {
        var team = OfficialUpdates.Team(role.Team);
        string[] suffixes = team switch
        {
            TeamIds.Minion or TeamIds.Demon => ["_e", string.Empty, "_g"],
            TeamIds.Traveler or TeamIds.Fabled or "loric" => [string.Empty, "_g", "_e"],
            _ => ["_g", string.Empty, "_e"],
        };

        var folder = role.Edition.Length > 0 ? role.Edition : team;
        return suffixes
            .Select(suffix => $"https://raw.githubusercontent.com/{Repository}/{reference}/resources/characters/{folder}/{role.Id}{suffix}.webp")
            .ToList();
    }

    public IReadOnlyList<OfficialRole> Roles { get; init; } = [];

    public IReadOnlyList<OfficialJinxes> Jinxes { get; init; } = [];

    public OfficialNightSheet NightSheet { get; init; } = new();

    /// <exception cref="JsonException">A file is not the published format.</exception>
    public static OfficialRelease Parse(string rolesJson, string jinxesJson, string nightSheetJson) => new()
    {
        Roles = JsonSerializer.Deserialize<List<OfficialRole>>(rolesJson, GameData.JsonOptions) ?? [],
        Jinxes = JsonSerializer.Deserialize<List<OfficialJinxes>>(jinxesJson, GameData.JsonOptions) ?? [],
        NightSheet = JsonSerializer.Deserialize<OfficialNightSheet>(nightSheetJson, GameData.JsonOptions) ?? new(),
    };
}
