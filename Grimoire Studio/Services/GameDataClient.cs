using System.Net.Http.Json;
using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// Loads the data folder served at <c>data/</c> once and caches it for the session; changes saved in this browser
/// (<see cref="LocalChanges"/>) are applied on every read.
/// </summary>
public sealed class GameDataClient(HttpClient http, LocalChanges local, TeamText teamText)
{
    private Task<GameData>? _load;

    public async Task<GameData> GetAsync()
    {
        var data = (await GetOriginalAsync())
            .WithTeams(await local.Teams.GetAllAsync())
            .WithRoles((await local.Roles.GetAllAsync()).Select(r => r.Role))
            .WithScripts(await local.Scripts.GetAllAsync())
            .WithJinxes(await local.Jinxes.GetAllAsync());

        teamText.Use(data.Teams);
        return data;
    }

    /// <summary>Data exactly as in the data folder, without local changes.</summary>
    public Task<GameData> GetOriginalAsync() => _load ??= LoadAsync();

    public static string IconUrl(string roleId) => $"data/icons/{roleId}.png";

    public static string EditionIconUrl(Script script) => $"data/editions/{script.IconName}.png";

    private async Task<GameData> LoadAsync()
    {
        var roles = ReadAsync<List<Role>>(GameData.RolesFile);
        var fabled = ReadAsync<List<Role>>(GameData.FabledFile);
        var scripts = ReadAsync<ScriptCatalog>(GameData.ScriptsFile);
        var jinxes = ReadAsync<List<RoleJinxes>>(GameData.JinxesFile);

        return new GameData
        {
            Roles = await roles,
            Fabled = await fabled,
            Scripts = await scripts,
            Jinxes = await jinxes,
        };
    }

    private async Task<T> ReadAsync<T>(string file) =>
        await http.GetFromJsonAsync<T>($"data/{file}", GameData.JsonOptions)
            ?? throw new InvalidDataException($"{file} is empty.");
}
