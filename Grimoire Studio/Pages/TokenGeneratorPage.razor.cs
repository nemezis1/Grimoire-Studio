using System.Globalization;
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

    private readonly TokenSettings _settings = new() { Text = "Nazwa" };
    private ElementReference _canvas;
    private GameData? _data;
    private Role? _role;
    private string? _uploadError;
    private List<TokenPreset> _presets = [];
    private string _selectedPreset = string.Empty;
    private string _presetName = string.Empty;
    private string? _presetMessage;

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
    }

    // Every UI change re-renders the component, so redrawing here keeps the preview live.
    protected override async Task OnAfterRenderAsync(bool firstRender) =>
        await Renderer.RenderAsync(_canvas, _settings, PreviewSize);

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
