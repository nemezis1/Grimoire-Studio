using Microsoft.JSInterop;

namespace Grimoire_Studio.Services;

/// <summary>Browser storage for local changes (IndexedDB) and the small helpers around it (localStorage, download).</summary>
public sealed class LocalStore(IJSRuntime js)
{
    private Task<IJSObjectReference>? _module;

    private Task<IJSObjectReference> Module =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/localStore.js").AsTask();

    public async Task<string?> GetAsync(string key) => await (await Module).InvokeAsync<string?>("dbGet", key);

    /// <returns>false when the browser refused to store (private mode, quota).</returns>
    public async Task<bool> SetAsync(string key, string value) => await (await Module).InvokeAsync<bool>("dbSet", key, value);

    /// <summary>
    /// Value saved under <paramref name="key"/> in localStorage by an earlier version, moved to IndexedDB; null when
    /// there is none.
    /// </summary>
    public async Task<string?> MigrateFromLocalStorageAsync(string key)
    {
        var module = await Module;
        var json = await module.InvokeAsync<string?>("getItem", key);
        if (json is not null && await SetAsync(key, json))
        {
            await module.InvokeVoidAsync("removeItem", key);
        }

        return json;
    }

    public async Task<string> ResizeImageAsync(string dataUrl, int maxSize) =>
        await (await Module).InvokeAsync<string>("resizeImage", dataUrl, maxSize);

    public async Task DownloadAsync(string fileName, string text) =>
        await (await Module).InvokeVoidAsync("downloadText", fileName, text);
}
