namespace Grimoire_Studio.Generator;

/// <summary>
/// Border of the player tokens of one team in a preset: colour, optional colour of the right half, thickness.
/// Mutable so the generator can bind it; presets store a copy.
/// </summary>
public sealed class TeamBorder
{
    public string Color { get; set; } = "#1E90FF";

    /// <summary>Colour of the right half; null draws a single-colour ring.</summary>
    public string? Color2 { get; set; }

    public int Thickness { get; set; } = 20;

    public TeamBorder Clone() => (TeamBorder)MemberwiseClone();

    public void ApplyTo(TokenSettings settings)
    {
        settings.BorderColor = Color;
        settings.BorderColor2 = Color2;
        settings.BorderThickness = Thickness;
    }
}
