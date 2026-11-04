using GrimoireStudio.Data;

namespace Grimoire_Studio.Printing;

/// <summary>Which team's colour a printed team is shown in.</summary>
public static class PrintColors
{
    /// <summary>
    /// The team whose colour <paramref name="team"/> takes: with <paramref name="goodEvil"/> outsiders share the
    /// townsfolk colour and minions the demon colour; other teams keep their own.
    /// </summary>
    public static string Team(string team, bool goodEvil) => goodEvil
        ? team switch
        {
            TeamIds.Outsider => TeamIds.Townsfolk,
            TeamIds.Minion => TeamIds.Demon,
            _ => team,
        }
        : team;
}
