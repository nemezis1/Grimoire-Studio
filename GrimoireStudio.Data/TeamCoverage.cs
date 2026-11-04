namespace GrimoireStudio.Data;

/// <summary>
/// One counted team of a script: <see cref="Count"/> roles, <see cref="Needed"/> at most in a game (15 players),
/// and the smallest player count it cannot fill (<see cref="ShortFrom"/>).
/// </summary>
public sealed record TeamCoverage(string Team, int Count, int Needed, int? ShortFrom);
