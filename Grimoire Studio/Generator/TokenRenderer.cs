using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Grimoire_Studio.Generator;

/// <summary>C# entry point to wwwroot/js/tokenRenderer.js (Canvas 2D drawing and per-browser presets).</summary>
public sealed class TokenRenderer(IJSRuntime js) : IAsyncDisposable
{
    private static readonly JsonSerializerOptions PresetJson = new(JsonSerializerDefaults.Web);

    private Task<IJSObjectReference>? _module;

    private Task<IJSObjectReference> Module =>
        _module ??= js.InvokeAsync<IJSObjectReference>("import", "./js/tokenRenderer.js").AsTask();

    public async Task RenderAsync(ElementReference canvas, TokenSettings settings, int size)
    {
        var module = await Module;
        await module.InvokeVoidAsync("render", canvas, settings, size);
    }

    public async Task DownloadPngAsync(TokenSettings settings, string fileName)
    {
        var module = await Module;
        await module.InvokeVoidAsync("downloadPng", settings, settings.ExportPixels, settings.Dpi, fileName);
    }

    /// <summary>Presets saved in this browser's localStorage; a damaged entry yields an empty list.</summary>
    public async Task<List<TokenPreset>> LoadPresetsAsync()
    {
        var module = await Module;
        var json = await module.InvokeAsync<string>("loadPresets");
        try
        {
            return JsonSerializer.Deserialize<List<TokenPreset>>(json, PresetJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <returns>false when the browser refused to store (private mode, quota).</returns>
    public async Task<bool> SavePresetsAsync(IEnumerable<TokenPreset> presets)
    {
        var module = await Module;
        return await module.InvokeAsync<bool>("savePresets", JsonSerializer.Serialize(presets, PresetJson));
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await _module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Page is going away; nothing to clean up.
            }
        }
    }
}
