using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Jinxes added, changed or removed in this browser, one entry per owner/other pair.</summary>
public sealed class LocalJinxes(LocalStore store) : LocalCollection<JinxOverride>(store, "grimoire-studio.local-jinxes")
{
    protected override string IdOf(JinxOverride item) => item.Key;
}
