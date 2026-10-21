using System.Globalization;
using System.Text.Json;
using Grimoire_Studio.Generator;
using GrimoireStudio.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Grimoire_Studio.Pages;

public partial class TokenGeneratorPage
{
    // Canvas resolution of the preview; displayed smaller via CSS for a crisp image.
    private const int PreviewSize = 800;
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private TokenSettings _settings = new() { Text = "Nazwa" };
    private ElementReference _canvas;
    private GameData? _data;
    private Role? _role;
    private string? _uploadError;
    private List<TokenPreset> _presets = [];
    private string _selectedPreset = string.Empty;
    private string _presetName = string.Empty;
    private string? _presetMessage;
    private readonly List<SheetItem> _sheet = [];
    private SheetItem? _editing;
    private CancellationTokenSource? _sheetRefresh;
    private bool _sheetSaveFailed;

    /// <summary>Role id to preload, e.g. /generator?role=imp (linked from the role page).</summary>
    [SupplyParameterFromQuery(Name = "role")]
    public string? RoleId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _presets = await Renderer.LoadPresetsAsync();
        _data = await Client.GetAsync();
        if (RoleId is not null && _data.FindRole(RoleId) is { } role)
        {
            ApplyRole(role);
        }

        foreach (var saved in await Renderer.LoadSheetAsync())
        {
            var item = new SheetItem(saved.Settings, saved.RoleId);
            _sheet.Add(item);
            await RefreshAsync(item);
        }
    }

    // Every UI change re-renders the component, so redrawing here keeps the preview live.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await Renderer.RenderAsync(_canvas, _settings, PreviewSize);

        // The sheet image of the token being edited follows the changes, shortly after they stop.
        if (_editing is { } item && Snapshot(item.Settings) != item.RenderedState)
        {
            _sheetRefresh?.Cancel();
            _sheetRefresh = new CancellationTokenSource();
            _ = RefreshLaterAsync(item, _sheetRefresh.Token);
        }
    }

    private void SelectRole(string? id)
    {
        if (_data?.FindRole(id ?? string.Empty) is { } role)
        {
            ApplyRole(role);
        }
        else
        {
            _role = null;
        }

        Nav.NavigateTo(Nav.GetUriWithQueryParameter("role", _role?.Id), replace: true);
    }

    private void ApplyRole(Role role)
    {
        _role = role;
        _settings.ApplyRole(role);
    }

    private void ToggleSecondColor(ChangeEventArgs e) =>
        _settings.BorderColor2 = e.Value is true ? "#B22222" : null;

    private void ClearImages()
    {
        _settings.ImageUrl = null;
        _settings.BackgroundUrl = null;
        _uploadError = null;
    }

    private async Task UploadAsync(InputFileChangeEventArgs e, Action<string> apply)
    {
        _uploadError = null;
        try
        {
            using var stream = e.File.OpenReadStream(MaxUploadBytes);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            apply($"data:{e.File.ContentType};base64,{Convert.ToBase64String(memory.ToArray())}");
        }
        catch (IOException)
        {
            _uploadError = $"Plik jest za duży (maksymalnie {MaxUploadBytes / 1024 / 1024} MB).";
        }
    }

    // Accepts "4,25" as well as "4.25"; out-of-range values are clamped, invalid input is ignored.
    private void SetSizeCm(ChangeEventArgs e)
    {
        var text = e.Value?.ToString()?.Replace(',', '.');
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cm))
        {
            _settings.SizeCm = Math.Round(Math.Clamp(cm, 1, 10), 2);
        }
    }

    private async Task DownloadAsync() => await Renderer.DownloadPngAsync(_settings, FileName());

    private void ApplyPreset(string? name)
    {
        _selectedPreset = name ?? string.Empty;
        _presetMessage = null;
        _presets.FirstOrDefault(p => p.Name == _selectedPreset)?.ApplyTo(_settings);
    }

    /// <summary>Restores the default look; text, description, pictures and leaf markers stay.</summary>
    private void ResetStyle()
    {
        var type = _settings.TokenType;
        new TokenPreset { Name = string.Empty, TokenType = type }.ApplyTo(_settings);
        if (type == TokenSettings.ReminderType)
        {
            _settings.TextVertical = TokenSettings.ReminderTextVertical;
        }

        _selectedPreset = string.Empty;
        _presetMessage = "Przywrócono ustawienia domyślne.";
    }

    private async Task SavePresetAsync()
    {
        var name = _presetName.Trim();
        var preset = TokenPreset.From(name, _settings);
        var index = _presets.FindIndex(p => p.Name == name);
        if (index >= 0)
        {
            _presets[index] = preset;
        }
        else
        {
            _presets.Add(preset);
            _presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        }

        _presetMessage = await Renderer.SavePresetsAsync(_presets)
            ? (index >= 0 ? $"Nadpisano preset „{name}”." : $"Zapisano preset „{name}”.")
            : "Przeglądarka nie pozwoliła zapisać presetu (tryb prywatny lub brak miejsca).";
        _selectedPreset = name;
        _presetName = string.Empty;
    }

    private async Task DeletePresetAsync()
    {
        _presets.RemoveAll(p => p.Name == _selectedPreset);
        _presetMessage = await Renderer.SavePresetsAsync(_presets)
            ? $"Usunięto preset „{_selectedPreset}”."
            : "Przeglądarka nie pozwoliła zapisać zmian.";
        _selectedPreset = string.Empty;
    }

    private async Task AddToSheetAsync()
    {
        var item = new SheetItem(_settings.Clone(), _role?.Id);
        _sheet.Add(item);
        await RefreshAsync(item);
        await SaveSheetAsync();
    }

    /// <summary>Loads a sheet token into the editor; every change applies to it directly.</summary>
    private void EditItem(SheetItem item)
    {
        _editing = item;
        _settings = item.Settings;
        _role = item.RoleId is null ? null : _data?.FindRole(item.RoleId);
        _selectedPreset = string.Empty;
    }

    private async Task FinishEditingAsync()
    {
        StopEditing();
        await SaveSheetAsync();
    }

    /// <summary>Stops editing; the editor keeps a copy of the token as a starting point for the next one.</summary>
    private void StopEditing()
    {
        if (_editing is not null)
        {
            _editing.RoleId = _role?.Id;
        }

        _settings = _settings.Clone();
        _editing = null;
    }

    private async Task DuplicateAsync(SheetItem item)
    {
        var copy = new SheetItem(item.Settings.Clone(), item.RoleId);
        _sheet.Insert(_sheet.IndexOf(item) + 1, copy);
        await RefreshAsync(copy);
        await SaveSheetAsync();
    }

    private async Task RemoveAsync(SheetItem item)
    {
        if (item == _editing)
        {
            StopEditing();
        }

        _sheet.Remove(item);
        await Renderer.ReleaseUrlAsync(item.ImageUrl);
        await SaveSheetAsync();
    }

    private async Task ClearSheetAsync()
    {
        StopEditing();
        foreach (var item in _sheet)
        {
            await Renderer.ReleaseUrlAsync(item.ImageUrl);
        }

        _sheet.Clear();
        await SaveSheetAsync();
    }

    // The sheet is kept in this browser (IndexedDB) so it survives a page reload.
    private async Task SaveSheetAsync() => _sheetSaveFailed = !await Renderer.SaveSheetAsync(_sheet);

    private async Task PrintAsync() => await Renderer.PrintAsync();

    private static string Snapshot(TokenSettings settings) => JsonSerializer.Serialize(settings);

    private static string TokenWord(int count) => count switch
    {
        1 => "token",
        _ when count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14 => "tokeny",
        _ => "tokenów",
    };

    private async Task RefreshAsync(SheetItem item)
    {
        var state = Snapshot(item.Settings);
        var previous = item.ImageUrl;
        item.ImageUrl = await Renderer.RenderToUrlAsync(item.Settings);
        item.RenderedState = state;
        await Renderer.ReleaseUrlAsync(previous);
    }

    private async Task RefreshLaterAsync(SheetItem item, CancellationToken cancellation)
    {
        try
        {
            await Task.Delay(250, cancellation);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (_sheet.Contains(item))
        {
            await RefreshAsync(item);
            await SaveSheetAsync();
            StateHasChanged();
        }
    }

    private string FileName()
    {
        var name = _role?.Id ?? _settings.Text;
        if (!_settings.IsPlayer)
        {
            name = $"{_role?.Id}-{_settings.Text}".Trim('-');
        }

        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return $"{(safe.Length == 0 ? "token" : safe)}.png";
    }
}
