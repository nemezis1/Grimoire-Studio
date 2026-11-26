namespace GrimoireStudio.Data;

/// <summary>A character as The Pandemonium Institute publishes it (botc-release, resources/data/roles.json).</summary>
public sealed record OfficialRole
{
    public required string Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Team as published; "traveller" there is <see cref="TeamIds.Traveler"/> here.</summary>
    public string Team { get; init; } = string.Empty;

    public string Edition { get; init; } = string.Empty;

    public string FirstNightReminder { get; init; } = string.Empty;

    public string OtherNightReminder { get; init; } = string.Empty;

    public IReadOnlyList<string> Reminders { get; init; } = [];

    public IReadOnlyList<string> RemindersGlobal { get; init; } = [];

    public bool Setup { get; init; }

    public string Ability { get; init; } = string.Empty;
}
