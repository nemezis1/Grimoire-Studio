namespace GrimoireStudio.Data;

/// <summary>A special rule between the owning role and the role <see cref="Id"/>.</summary>
public sealed record Jinx
{
    public required string Id { get; init; }
    public string ReasonEn { get; init; } = string.Empty;
    public string ReasonPl { get; init; } = string.Empty;
}
