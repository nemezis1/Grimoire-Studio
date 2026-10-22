using System.Text.Json;
using GrimoireStudio.Data;
using Microsoft.JSInterop;

namespace Grimoire_Studio.Services;

/// <summary>
/// Scripts created or changed in this browser (localStorage). They override scripts with the same id from the
/// data folder and are shown everywhere in the wiki.
/// </summary>
public sealed class LocalScripts(IJSRuntime js)
{
    private const string StorageKey = "grimoire-studio.local-scripts";

    private Task<IJSObjectReference>? _module;
    private List<Script>? _scripts;

    private Task<IJSObjectReference> Module =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/localStore.js").AsTask();

    public async Task<IReadOnlyList<Script>> GetAllAsync() => await LoadAsync();

    public async Task<bool> IsLocalAsync(string id) =>
        (await LoadAsync()).Any(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <returns>false when the browser refused to store.</returns>
    public async Task<bool> SaveAsync(Script script)
    {
        var scripts = await LoadAsync();
        scripts.RemoveAll(s => string.Equals(s.Id, script.Id, StringComparison.OrdinalIgnoreCase));
        scripts.Add(script);
        return await PersistAsync(scripts);
    }

    /// <summary>Removes the local version: a new script disappears, an edited one returns to the original.</summary>
    public async Task<bool> RemoveAsync(string id)
    {
        var scripts = await LoadAsync();
        scripts.RemoveAll(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        return await PersistAsync(scripts);
    }

    private async Task<List<Script>> LoadAsync()
    {
        if (_scripts is not null)
        {
            return _scripts;
        }

        var module = await Module;
        var json = await module.InvokeAsync<string?>("getItem", StorageKey);
        try
        {
            _scripts = json is null ? [] : JsonSerializer.Deserialize<List<Script>>(json, GameData.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            _scripts = [];
        }

        return _scripts;
    }

    private async Task<bool> PersistAsync(List<Script> scripts)
    {
        var module = await Module;
        return await module.InvokeAsync<bool>("setItem", StorageKey, JsonSerializer.Serialize(scripts, GameData.JsonOptions));
    }
}
