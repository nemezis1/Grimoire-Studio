using System.Net.Http.Json;
using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// Loads the data folder served at <c>data/</c> once and caches it for the session. Reads apply the change sets of
/// this browser (<see cref="ChangeSets"/>): first the wiki, then the visitor's own changes.
/// </summary>
public sealed class GameDataClient(HttpClient http, ChangeSets changes, TeamText teamText)
{
    private Task<GameData>? _load;

    /// <summary>Data as shown: data folder, wiki changes and personal changes.</summary>
    public async Task<GameData> GetAsync()
    {
        var wiki = await GetWikiAsync();
        await changes.Personal.PruneAsync(wiki);
        var data = await changes.Personal.ApplyToAsync(wiki);
        teamText.Use(data.Teams);
        return data;
    }

    /// <summary>Data exactly as in the data folder, without any changes.</summary>
    public Task<GameData> GetOriginalAsync() => _load ??= LoadAsync();

    /// <summary>Data folder with the wiki changes (approved proposals and direct admin edits).</summary>
    public async Task<GameData> GetWikiAsync()
    {
        await changes.LoadAsync();
        var original = await GetOriginalAsync();
        await changes.Wiki.PruneAsync(original);
        return await changes.Wiki.ApplyToAsync(original);
    }

    /// <summary>
    /// What the current change set is compared with: the data folder while editing the wiki, otherwise the wiki.
    /// </summary>
    public async Task<GameData> GetBaseAsync()
    {
        await changes.LoadAsync();
        return changes.EditingWiki ? await GetOriginalAsync() : await GetWikiAsync();
    }

    public static string IconUrl(string roleId) => $"data/icons/{roleId}.png";

    /// <summary>The script's uploaded picture, or its edition icon from the data folder.</summary>
    public static string EditionIconUrl(Script script) => script.Image ?? EditionIconUrl(script.IconName);

    public static string EditionIconUrl(string iconName) => $"data/editions/{iconName}.png";

    private async Task<GameData> LoadAsync()
    {
        var roles = ReadAsync<List<Role>>(GameData.RolesFile);
        var fabled = ReadAsync<List<Role>>(GameData.FabledFile);
        var scripts = ReadAsync<ScriptCatalog>(GameData.ScriptsFile);
        var jinxes = ReadAsync<List<RoleJinxes>>(GameData.JinxesFile);
        var teams = ReadAsync<List<TeamInfo>>(GameData.TeamsFile);
        var version = ReadVersionAsync();

        return new GameData
        {
            Roles = await roles,
            Fabled = await fabled,
            Scripts = await scripts,
            Jinxes = await jinxes,
            Teams = await teams,
            Version = await version,
        };
    }

    private async Task<string> ReadVersionAsync()
    {
        try
        {
            return (await http.GetStringAsync($"data/{GameData.VersionFile}")).Trim();
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }

    private async Task<T> ReadAsync<T>(string file) =>
        await http.GetFromJsonAsync<T>($"data/{file}", GameData.JsonOptions)
            ?? throw new InvalidDataException($"{file} is empty.");
}
