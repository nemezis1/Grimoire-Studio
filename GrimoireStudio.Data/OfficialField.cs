namespace GrimoireStudio.Data;

/// <summary>
/// One field of a role that differs from the published one. Lists (reminder tokens) are kept one item a line. Text
/// fields carry the Polish text the role has now (<see cref="Polish"/>), to be checked against the new English one.
/// </summary>
public sealed record OfficialField
{
    public const string Name = "name";
    public const string Ability = "ability";
    public const string FirstNightReminder = "firstNightReminder";
    public const string OtherNightReminder = "otherNightReminder";
    public const string Reminders = "reminders";
    public const string RemindersGlobal = "remindersGlobal";
    public const string Team = "team";
    public const string Setup = "setup";
    public const string Official = "official";

    public required string Key { get; init; }

    /// <summary>What the field is, in Polish.</summary>
    public required string Label { get; init; }

    /// <summary>The value the role has now (empty for a new role).</summary>
    public string Old { get; init; } = string.Empty;

    /// <summary>The published value.</summary>
    public string New { get; init; } = string.Empty;

    /// <summary>The role's Polish text for this field now, null for a field without one (team, setup, official).</summary>
    public string? Polish { get; init; }

    /// <summary>A list of reminder tokens, one a line.</summary>
    public bool IsList => Key is Reminders or RemindersGlobal;

    public bool HasPolish => Polish is not null;
}
