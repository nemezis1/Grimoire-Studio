namespace Grimoire_Studio.Generator;

/// <summary>
/// Saved look of a token (fonts, sizes, colours, positions, print size). Content — text, description, pictures and
/// leaf markers — is not part of a preset, so one preset can be applied to every role.
/// </summary>
public sealed record TokenPreset
{
    public required string Name { get; init; }
    public string TokenType { get; init; } = TokenSettings.PlayerType;

    public string Font { get; init; } = TokenFonts.DefaultLabel;
    public int FontSize { get; init; } = 40;
    public double LetterSpacing { get; init; }
    public double TextVertical { get; init; }
    public string TextColor { get; init; } = "#000000";

    public string DescriptionFont { get; init; } = TokenFonts.DefaultDescription;
    public int DescriptionFontSize { get; init; } = 25;
    public double DescriptionVertical { get; init; }

    public double ImageScale { get; init; } = 1.0;
    public double ImageVertical { get; init; }
    public double ImageHorizontal { get; init; }

    public string BorderColor { get; init; } = "#1E90FF";
    public string? BorderColor2 { get; init; }
    public int BorderThickness { get; init; } = 20;
    public double LeafScale { get; init; } = 1.0;

    public double PlayerSizeCm { get; init; } = TokenSettings.DefaultPlayerSizeCm;
    public double ReminderSizeCm { get; init; } = TokenSettings.DefaultReminderSizeCm;
    public int Dpi { get; init; } = 300;

    public static TokenPreset From(string name, TokenSettings s) => new()
    {
        Name = name,
        TokenType = s.TokenType,
        Font = s.Font,
        FontSize = s.FontSize,
        LetterSpacing = s.LetterSpacing,
        TextVertical = s.TextVertical,
        TextColor = s.TextColor,
        DescriptionFont = s.DescriptionFont,
        DescriptionFontSize = s.DescriptionFontSize,
        DescriptionVertical = s.DescriptionVertical,
        ImageScale = s.ImageScale,
        ImageVertical = s.ImageVertical,
        ImageHorizontal = s.ImageHorizontal,
        BorderColor = s.BorderColor,
        BorderColor2 = s.BorderColor2,
        BorderThickness = s.BorderThickness,
        LeafScale = s.LeafScale,
        PlayerSizeCm = s.PlayerSizeCm,
        ReminderSizeCm = s.ReminderSizeCm,
        Dpi = s.Dpi,
    };

    public void ApplyTo(TokenSettings s)
    {
        s.TokenType = TokenType;
        s.Font = Font;
        s.FontSize = FontSize;
        s.LetterSpacing = LetterSpacing;
        s.TextVertical = TextVertical;
        s.TextColor = TextColor;
        s.DescriptionFont = DescriptionFont;
        s.DescriptionFontSize = DescriptionFontSize;
        s.DescriptionVertical = DescriptionVertical;
        s.ImageScale = ImageScale;
        s.ImageVertical = ImageVertical;
        s.ImageHorizontal = ImageHorizontal;
        s.BorderColor = BorderColor;
        s.BorderColor2 = BorderColor2;
        s.BorderThickness = BorderThickness;
        s.LeafScale = LeafScale;
        s.PlayerSizeCm = PlayerSizeCm;
        s.ReminderSizeCm = ReminderSizeCm;
        s.Dpi = Dpi;
    }
}
