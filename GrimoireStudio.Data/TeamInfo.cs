namespace GrimoireStudio.Data;

/// <summary>A team (character type): its names, colour and place in lists. Stored in teams.json; can be changed locally and new ones added.</summary>
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
