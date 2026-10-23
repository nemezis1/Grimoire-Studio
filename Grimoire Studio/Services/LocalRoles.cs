using System.Text.Json;
using GrimoireStudio.Data;
using Microsoft.JSInterop;

namespace Grimoire_Studio.Services;

/// <summary>
/// Roles created or changed in this browser (localStorage). They override roles with the same id from the data
/// folder and are shown everywhere: wiki, script editor, printouts and the token generator.
/// </summary>
public sealed class LocalRoles(IJSRuntime js)
{
    private const string StorageKey = "grimoire-studio.local-roles";

    private Task<IJSObjectReference>? _module;
    private List<LocalRole>? _roles;

    private Task<IJSObjectReference> Module =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/localStore.js").AsTask();

    public async Task<IReadOnlyList<LocalRole>> GetAllAsync() => await LoadAsync();

    public async Task<LocalRole?> FindAsync(string id) =>
        (await LoadAsync()).FirstOrDefault(r => string.Equals(r.Role.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Icon of a role: the uploaded one when the role was saved locally with an icon, otherwise the data folder's.
    /// Synchronous for rendering; local roles are loaded together with the game data.
    /// </summary>
    public string IconUrl(string roleId) =>
        _roles?.FirstOrDefault(r => string.Equals(r.Role.Id, roleId, StringComparison.OrdinalIgnoreCase))?.IconUrl
            ?? GameDataClient.IconUrl(roleId);

    /// <returns>false when the browser refused to store (private mode, quota).</returns>
    public async Task<bool> SaveAsync(LocalRole role)
    {
        var roles = await LoadAsync();
        roles.RemoveAll(r => string.Equals(r.Role.Id, role.Role.Id, StringComparison.OrdinalIgnoreCase));
        roles.Add(role);
        return await PersistAsync(roles);
    }

    /// <summary>Uploaded icons are scaled down so many local roles still fit in localStorage.</summary>
    public async Task<string> ShrinkIconAsync(string dataUrl, int maxSize = 256)
    {
        var module = await Module;
        return await module.InvokeAsync<string>("resizeImage", dataUrl, maxSize);
    }

    /// <summary>Removes the local version: a new role disappears, an edited one returns to the original.</summary>
    public async Task<bool> RemoveAsync(string id)
    {
        var roles = await LoadAsync();
        roles.RemoveAll(r => string.Equals(r.Role.Id, id, StringComparison.OrdinalIgnoreCase));
        return await PersistAsync(roles);
    }

    private async Task<List<LocalRole>> LoadAsync()
    {
        if (_roles is not null)
        {
            return _roles;
        }

        var module = await Module;
        var json = await module.InvokeAsync<string?>("getItem", StorageKey);
        try
        {
            _roles = json is null ? [] : JsonSerializer.Deserialize<List<LocalRole>>(json, GameData.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            _roles = [];
        }

        return _roles;
    }

    private async Task<bool> PersistAsync(List<LocalRole> roles)
    {
        var module = await Module;
        return await module.InvokeAsync<bool>("setItem", StorageKey, JsonSerializer.Serialize(roles, GameData.JsonOptions));
    }
}
