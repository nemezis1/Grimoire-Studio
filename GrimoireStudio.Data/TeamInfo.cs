namespace GrimoireStudio.Data;

/// <summary>A team (character type): its names, colour and place in lists. Built-in teams can be changed locally and new ones added.</summary>
public sealed record TeamInfo
{
    public required string Id { get; init; }

    /// <summary>Singular name, e.g. "Mieszkaniec".</summary>
    public required string NamePl { get; init; }
    public required string NameEn { get; init; }

    /// <summary>Plural name used for headings, e.g. "Mieszkańcy".</summary>
    public required string PluralPl { get; init; }
    public required string PluralEn { get; init; }

    /// <summary>CSS colour, e.g. "#2f6fd6".</summary>
    public string Color { get; init; } = "#888888";

    /// <summary>Position in lists and printouts; lower comes first.</summary>
    public int Order { get; init; }

    /// <summary>Outside the character count (travellers, fabled): printed on the separate extras page.</summary>
    public bool Extra { get; init; }

    public static IReadOnlyList<TeamInfo> Defaults { get; } =
    [
        new() { Id = TeamIds.Townsfolk, NamePl = "Mieszkaniec", NameEn = "Townsfolk", PluralPl = "Mieszkańcy", PluralEn = "Townsfolk", Color = "#2f6fd6", Order = 10 },
        new() { Id = TeamIds.Outsider, NamePl = "Przybysz", NameEn = "Outsider", PluralPl = "Przybysze", PluralEn = "Outsiders", Color = "#2aa3c7", Order = 20 },
        new() { Id = TeamIds.Minion, NamePl = "Sługus", NameEn = "Minion", PluralPl = "Sługusy", PluralEn = "Minions", Color = "#d96a1a", Order = 30 },
        new() { Id = TeamIds.Demon, NamePl = "Demon", NameEn = "Demon", PluralPl = "Demony", PluralEn = "Demons", Color = "#c42020", Order = 40 },
        new() { Id = TeamIds.Traveler, NamePl = "Wędrowiec", NameEn = "Traveller", PluralPl = "Wędrowcy", PluralEn = "Travellers", Color = "#9a3fc4", Order = 50, Extra = true },
        new() { Id = TeamIds.Fabled, NamePl = "Legenda", NameEn = "Fabled", PluralPl = "Legendy", PluralEn = "Fabled", Color = "#b8960c", Order = 60, Extra = true },
    ];

    /// <summary>Stand-in for a team id that is not defined (e.g. a local team that was removed).</summary>
    public static TeamInfo Unknown(string id) => new()
    {
        Id = id,
        NamePl = id,
        NameEn = id,
        PluralPl = id,
        PluralEn = id,
        Order = int.MaxValue,
        Extra = true,
    };
}
