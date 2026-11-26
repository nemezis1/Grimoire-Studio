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
