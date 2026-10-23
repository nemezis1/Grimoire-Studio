using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Teams added or changed in this browser; a built-in team with the same id is replaced.</summary>
public sealed class LocalTeams(LocalStore store) : LocalCollection<TeamInfo>(store, "grimoire-studio.local-teams")
{
    protected override string IdOf(TeamInfo item) => item.Id;
}
