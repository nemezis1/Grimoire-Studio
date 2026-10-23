namespace GrimoireStudio.Data;

/// <summary>
/// Result of reading an official Script Tool JSON: the script, homebrew roles, teams and jinxes it defines that are
/// not in the data yet, and role ids that could not be resolved (left out of the script).
/// </summary>
public sealed record ScriptToolImport(
    Script Script,
    IReadOnlyList<Role> NewRoles,
    IReadOnlyList<TeamInfo> NewTeams,
    IReadOnlyList<JinxOverride> NewJinxes,
    IReadOnlyList<string> UnknownRoles);
