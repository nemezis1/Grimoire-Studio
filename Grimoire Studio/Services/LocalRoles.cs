using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Roles created or changed in one change set; they override roles with the same id.</summary>
public sealed class LocalRoles(LocalStore store, string prefix) : LocalCollection<LocalRole>(store, $"{prefix}-roles")
{
    protected override string IdOf(LocalRole item) => item.Role.Id;

    /// <summary>
    /// Uploaded icon of a role saved in this change set, or null. Synchronous for rendering; change sets are loaded
    /// together with the game data.
    /// </summary>
    public string? IconUrl(string roleId) =>
        Loaded.FirstOrDefault(r => string.Equals(r.Role.Id, roleId, StringComparison.OrdinalIgnoreCase))?.IconUrl;

    /// <summary>Uploaded icons are scaled down to keep the stored data small.</summary>
    public Task<string> ShrinkIconAsync(string dataUrl, int maxSize = 256) => Store.ResizeImageAsync(dataUrl, maxSize);
}
