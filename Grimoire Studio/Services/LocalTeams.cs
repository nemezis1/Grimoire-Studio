using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Teams added or changed in one change set; a team with the same id is replaced.</summary>
public sealed class LocalTeams(LocalStore store, string prefix) : LocalCollection<TeamInfo>(store, $"{prefix}-teams")
{
    protected override string IdOf(TeamInfo item) => item.Id;
}
