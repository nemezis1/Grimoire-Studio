namespace GrimoireStudio.Data;

/// <summary>Standard role distribution for a number of players (base game rules, before setup modifiers).</summary>
public sealed record PlayerCount(int Players, int Townsfolk, int Outsiders, int Minions, int Demons)
{
    public const int MinPlayers = 5;
    public const int MaxPlayers = 15;

    /// <summary>Distributions for 5 to 15 players; 15+ players use the 15-player row plus travellers.</summary>
    public static IReadOnlyList<PlayerCount> Table { get; } =
    [
        new(5, 3, 0, 1, 1),
        new(6, 3, 1, 1, 1),
        new(7, 5, 0, 1, 1),
        new(8, 5, 1, 1, 1),
        new(9, 5, 2, 1, 1),
        new(10, 7, 0, 2, 1),
        new(11, 7, 1, 2, 1),
        new(12, 7, 2, 2, 1),
        new(13, 9, 0, 3, 1),
        new(14, 9, 1, 3, 1),
        new(15, 9, 2, 3, 1),
    ];
}
