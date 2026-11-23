using System.Text.Json;

namespace Grimoire_Studio.Services;

/// <summary>
/// Roles this visitor likes (♥) or dislikes (☠), kept in this browser. The role list hides disliked roles unless asked
/// to show them, and can list only the liked ones.
/// </summary>
public sealed class RolePreferences(LocalStore store)
{
    public const string Like = "like";
    public const string Dislike = "dislike";

    private const string StorageKey = "grimoire-studio.role-preferences";

    private Dictionary<string, string> _byRole = new(StringComparer.OrdinalIgnoreCase);
    private Task? _load;

    /// <summary>Raised after a role is marked or unmarked.</summary>
    public event Action? Changed;

    public Task LoadAsync() => _load ??= LoadCoreAsync();

    private async Task LoadCoreAsync()
    {
        try
        {
            var json = await store.GetSettingAsync(StorageKey);
            var saved = json is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            _byRole = new Dictionary<string, string>(
                (saved ?? []).Where(p => p.Value is Like or Dislike),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            _byRole = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary><see cref="Like"/>, <see cref="Dislike"/> or null; empty until <see cref="LoadAsync"/> has run.</summary>
    public string? Get(string roleId) => _byRole.GetValueOrDefault(roleId);

    public bool IsLiked(string roleId) => Get(roleId) == Like;

    public bool IsDisliked(string roleId) => Get(roleId) == Dislike;

    /// <summary>Marks the role with <paramref name="preference"/>, or unmarks it when it already has it.</summary>
    public async Task ToggleAsync(string roleId, string preference)
    {
        await LoadAsync();
        if (Get(roleId) == preference)
        {
            _byRole.Remove(roleId);
        }
        else
        {
            _byRole[roleId] = preference;
        }

        await store.SetSettingAsync(StorageKey, JsonSerializer.Serialize(_byRole));
        Changed?.Invoke();
    }
}
