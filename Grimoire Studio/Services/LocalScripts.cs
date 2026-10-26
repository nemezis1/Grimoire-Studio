using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Scripts created or changed in one change set; they override scripts with the same id.</summary>
public sealed class LocalScripts(LocalStore store, string prefix) : LocalCollection<Script>(store, $"{prefix}-scripts")
{
    protected override string IdOf(Script item) => item.Id;

    public async Task<bool> IsLocalAsync(string id) => await FindAsync(id) is not null;
}
