namespace Grimoire_Studio.Services;

/// <summary>
/// Roles created or changed in this browser. They override roles with the same id from the data folder and are
/// shown everywhere: wiki, script editor, printouts and the token generator.
/// </summary>
public sealed class LocalRoles(LocalStore store) : LocalCollection<LocalRole>(store, "grimoire-studio.local-roles")
{
    protected override string IdOf(LocalRole item) => item.Role.Id;

    /// <summary>
    /// Icon of a role: the uploaded one when the role was saved locally with an icon, otherwise the data folder's.
    /// Synchronous for rendering; local roles are loaded together with the game data.
    /// </summary>
    public string IconUrl(string roleId) =>
        Loaded.FirstOrDefault(r => string.Equals(r.Role.Id, roleId, StringComparison.OrdinalIgnoreCase))?.IconUrl
            ?? GameDataClient.IconUrl(roleId);

    /// <summary>Uploaded icons are scaled down to keep the stored data small.</summary>
    public Task<string> ShrinkIconAsync(string dataUrl, int maxSize = 256) => Store.ResizeImageAsync(dataUrl, maxSize);
}
