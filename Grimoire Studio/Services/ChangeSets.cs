namespace Grimoire_Studio.Services;

/// <summary>
/// The two change sets of this browser. <see cref="Wiki"/> holds approved changes and the admin's direct edits (later
/// they will go to the repository on GitHub); <see cref="Personal"/> holds the visitor's own changes on top of the wiki.
/// Editors save to <see cref="Current"/>: the wiki while the admin edits it directly, otherwise the personal set.
/// </summary>
public sealed class ChangeSets(LocalStore store)
{
    /// <summary>Query parameter that opens the admin mode, e.g. <c>/?admin</c>.</summary>
    public const string AdminParameter = "admin";

    private const string AdminKey = "grimoire-studio.admin";
    private const string EditingWikiKey = "grimoire-studio.editing-wiki";

    private Task? _load;

    public LocalChanges Personal { get; } = new(store, "grimoire-studio.local");
    public LocalChanges Wiki { get; } = new(store, "grimoire-studio.wiki");

    /// <summary>
    /// Admin mode: opened with <see cref="AdminParameter"/> in the address and kept for this tab's browsing session
    /// (sessionStorage) until left. Not a protection yet: everything stays in this browser until changes go to GitHub.
    /// </summary>
    public bool IsAdmin { get; private set; }

    /// <summary>Editors save straight to the wiki (admin only).</summary>
    public bool EditingWiki { get; private set; }

    public LocalChanges Current => EditingWiki ? Wiki : Personal;

    /// <summary><see cref="Current"/> after the admin switches are read, so a page opened directly gets the right set.</summary>
    public async Task<LocalChanges> GetCurrentAsync()
    {
        await LoadAsync();
        return Current;
    }

    /// <summary>Raised when the admin switches change; the layout shows the editing mode.</summary>
    public event Action? Changed;

    public Task LoadAsync() => _load ??= LoadCoreAsync();

    private async Task LoadCoreAsync()
    {
        IsAdmin = await store.GetSessionAsync(AdminKey) == "1";
        EditingWiki = IsAdmin && await store.GetSessionAsync(EditingWikiKey) == "1";

        // Earlier versions kept these switches in localStorage, i.e. forever.
        await store.RemoveSettingAsync(AdminKey);
        await store.RemoveSettingAsync(EditingWikiKey);
        Changed?.Invoke();
    }

    public async Task SetAdminAsync(bool on)
    {
        await LoadAsync();
        IsAdmin = on;
        await store.SetSessionAsync(AdminKey, on ? "1" : null);
        if (!on)
        {
            await SetEditingWikiAsync(false);
        }

        Changed?.Invoke();
    }

    public async Task SetEditingWikiAsync(bool on)
    {
        EditingWiki = on && IsAdmin;
        await store.SetSessionAsync(EditingWikiKey, EditingWiki ? "1" : null);
        Changed?.Invoke();
    }

    /// <summary>True when the address has <see cref="AdminParameter"/> (any value except 0 / false).</summary>
    public static bool HasAdminParameter(string uri)
    {
        var query = new Uri(uri).Query.TrimStart('?');
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Any(kv => string.Equals(kv[0], AdminParameter, StringComparison.OrdinalIgnoreCase)
                && (kv.Length == 1 || kv[1] is not ("0" or "false")));
    }

    /// <summary>Icon of a role: personal upload, then wiki upload, then the data folder's file.</summary>
    public string IconUrl(string roleId) =>
        Personal.Roles.IconUrl(roleId) ?? Wiki.Roles.IconUrl(roleId) ?? GameDataClient.IconUrl(roleId);
}
