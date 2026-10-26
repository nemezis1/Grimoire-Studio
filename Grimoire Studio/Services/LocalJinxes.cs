using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Jinxes added, changed or removed in one change set, one entry per owner/other pair.</summary>
public sealed class LocalJinxes(LocalStore store, string prefix) : LocalCollection<JinxOverride>(store, $"{prefix}-jinxes")
{
    protected override string IdOf(JinxOverride item) => item.Key;
}
