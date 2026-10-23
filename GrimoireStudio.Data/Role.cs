using System.Text.Json.Serialization;

namespace GrimoireStudio.Data;

/// <summary>
/// A character. JSON shape matches Blood.Core/Data/roles.json so Blood can consume the same files.
/// </summary>
public sealed record Role
{
    public required string Id { get; init; }

    public required string NameEn { get; init; }
    public required string NamePl { get; init; }

    /// <summary>Home script id (tb, bmr, snv) or empty for experimental / other roles.</summary>
    public string Edition { get; init; } = string.Empty;

    /// <summary>Team id: one of <see cref="TeamIds"/> or a team added locally.</summary>
    public string Team { get; init; } = TeamIds.Townsfolk;

    public required string AbilityEn { get; init; }
    public required string AbilityPl { get; init; }

    /// <summary>Position in the first-night order; 0 means the role does not wake.</summary>
    public int FirstNight { get; init; }

    /// <summary>Position in the other-nights order; 0 means the role does not wake.</summary>
    public int OtherNight { get; init; }

    public string FirstNightReminderEn { get; init; } = string.Empty;
    public string FirstNightReminderPl { get; init; } = string.Empty;
    public string OtherNightReminderEn { get; init; } = string.Empty;
    public string OtherNightReminderPl { get; init; } = string.Empty;

    public IReadOnlyList<string> RemindersEn { get; init; } = [];
    public IReadOnlyList<string> RemindersPl { get; init; } = [];

    public IReadOnlyList<string> RemindersGlobalEn { get; init; } = [];
    public IReadOnlyList<string> RemindersGlobalPl { get; init; } = [];

    /// <summary>The role modifies game setup (e.g. Baron changes the outsider count).</summary>
    public bool Setup { get; init; }

    [JsonIgnore]
    public bool WakesFirstNight => FirstNight > 0;

    [JsonIgnore]
    public bool WakesOtherNights => OtherNight > 0;
}
