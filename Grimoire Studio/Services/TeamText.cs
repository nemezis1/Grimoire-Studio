using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// Team labels and colours for rendering. Holds the teams of the last loaded game data (teams.json plus local), so
/// components can look them up synchronously.
/// </summary>
public sealed class TeamText
{
    private IReadOnlyList<TeamInfo> _teams = [];

    /// <summary>Teams in list order.</summary>
    public IReadOnlyList<TeamInfo> All => _teams;

    /// <summary>Called by <see cref="GameDataClient"/> whenever it hands out data.</summary>
    public void Use(IReadOnlyList<TeamInfo> teams) => _teams = teams;

    public TeamInfo Get(string id) =>
        _teams.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? TeamInfo.Unknown(id);

    public string Plural(string id) => Get(id).PluralPl;

    public string Singular(string id) => Get(id).NamePl;

    /// <summary>Inline style giving the element its team colour (CSS variable <c>--team</c>).</summary>
    public string Style(string id) => $"--team: {Get(id).Color}";

    public TeamInfo? Parse(string? id) =>
        _teams.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
}
