using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Keys (<see cref="LocalBackup.Removed"/>) of the roles, scripts and teams one change set removes.</summary>
public sealed class LocalRemovals(LocalStore store, string prefix) : LocalCollection<string>(store, $"{prefix}-removed")
{
    protected override string IdOf(string item) => item;
}
