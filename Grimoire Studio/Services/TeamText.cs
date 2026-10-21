using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Polish team labels (same wording as Blood) and the CSS modifier used for team colours.</summary>
public static class TeamText
{
    /// <summary>Teams in sheet order.</summary>
    public static readonly IReadOnlyList<Team> All =
        [Team.Townsfolk, Team.Outsider, Team.Minion, Team.Demon, Team.Traveler, Team.Fabled];

    public static string Plural(Team team) => team switch
    {
        Team.Townsfolk => "Mieszkańcy",
        Team.Outsider => "Przybysze",
        Team.Minion => "Sługusy",
        Team.Demon => "Demony",
        Team.Traveler => "Wędrowcy",
        Team.Fabled => "Legendy",
        _ => "Nieznani",
    };

    public static string Singular(Team team) => team switch
    {
        Team.Townsfolk => "Mieszkaniec",
        Team.Outsider => "Przybysz",
        Team.Minion => "Sługus",
        Team.Demon => "Demon",
        Team.Traveler => "Wędrowiec",
        Team.Fabled => "Legenda",
        _ => "Nieznany",
    };

    public static string Slug(Team team) => team.ToString().ToLowerInvariant();

    public static Team? Parse(string? slug) =>
        Enum.TryParse<Team>(slug, ignoreCase: true, out var team) && Enum.IsDefined(team) ? team : null;
}
