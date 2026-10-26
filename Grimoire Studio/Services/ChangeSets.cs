namespace Grimoire_Studio.Services;

/// <summary>
/// The two change sets of this browser. <see cref="Wiki"/> holds approved changes and the admin's direct edits (later
/// they will go to the repository on GitHub); <see cref="Personal"/> holds the visitor's own changes on top of the wiki.
/// Editors save to <see cref="Current"/>: the wiki while the admin edits it directly, otherwise the personal set.
/// </summary>
public sealed class ChangeSets(LocalStore store)
{
    private const string AdminKey = "grimoire-studio.admin";
    private const string EditingWikiKey = "grimoire-studio.editing-wiki";

    private bool _loaded;

    public LocalChanges Personal { get; } = new(store, "grimoire-studio.local");
    public LocalChanges Wiki { get; } = new(store, "grimoire-studio.wiki");

    /// <summary>
    /// Admin panel enabled in this browser. Only a switch for now: everything stays in this browser, so there is
    /// nothing to protect until changes are sent to GitHub.
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

    public async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        IsAdmin = await store.GetSettingAsync(AdminKey) == "1";
        EditingWiki = IsAdmin && await store.GetSettingAsync(EditingWikiKey) == "1";
        _loaded = true;
        Changed?.Invoke();
    }

    public async Task SetAdminAsync(bool on)
    {
        IsAdmin = on;
        await store.SetSettingAsync(AdminKey, on ? "1" : "0");
        if (!on)
        {
            await SetEditingWikiAsync(false);
        }

        Changed?.Invoke();
    }

    public async Task SetEditingWikiAsync(bool on)
    {
        EditingWiki = on && IsAdmin;
        await store.SetSettingAsync(EditingWikiKey, EditingWiki ? "1" : "0");
        Changed?.Invoke();
    }

    /// <summary>Icon of a role: personal upload, then wiki upload, then the data folder's file.</summary>
    public string IconUrl(string roleId) =>
        Personal.Roles.IconUrl(roleId) ?? Wiki.Roles.IconUrl(roleId) ?? GameDataClient.IconUrl(roleId);
}
