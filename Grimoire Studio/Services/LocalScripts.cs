using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// Scripts created or changed in this browser. They override scripts with the same id from the data folder and are
/// shown everywhere in the wiki.
/// </summary>
public sealed class LocalScripts(LocalStore store) : LocalCollection<Script>(store, "grimoire-studio.local-scripts")
{
    protected override string IdOf(Script item) => item.Id;

    public async Task<bool> IsLocalAsync(string id) => await FindAsync(id) is not null;
}
