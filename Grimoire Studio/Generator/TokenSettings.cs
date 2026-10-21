using GrimoireStudio.Data;

namespace Grimoire_Studio.Generator;

/// <summary>
/// Everything needed to draw one token. Serialized (camelCase) to wwwroot/js/tokenRenderer.js.
/// Pixel values are for a 500 px token; the renderer scales them to the output size.
/// </summary>
public sealed class TokenSettings
{
    public const string PlayerType = "player";
    public const string ReminderType = "reminder";

    /// <summary>Reminder text sits under the icon; for player tokens the same setting moves the curved name.</summary>
    public const double ReminderTextVertical = 0.3;

    /// <summary><see cref="PlayerType"/> (curved name, border, leaves, description) or <see cref="ReminderType"/>.</summary>
    public string TokenType { get; set; } = PlayerType;

    public string Text { get; set; } = string.Empty;
    public string Font { get; set; } = TokenFonts.DefaultLabel;
    public int FontSize { get; set; } = 40;
    public double LetterSpacing { get; set; }
    public double TextVertical { get; set; }
    public string TextColor { get; set; } = "#000000";

    public string Description { get; set; } = string.Empty;
    public string DescriptionFont { get; set; } = TokenFonts.DefaultDescription;
    public int DescriptionFontSize { get; set; } = 20;

    /// <summary>Shift (fraction of the radius) from the automatic position below the border and top leaves.</summary>
    public double DescriptionVertical { get; set; }

    /// <summary>Picture in the middle: a role icon URL or an uploaded data URL.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>1 fills the free space between description and name; offsets move it from there.</summary>
    public double ImageScale { get; set; } = 1.0;
    public double ImageVertical { get; set; }
    public double ImageHorizontal { get; set; }

    /// <summary>Custom background (data URL); empty means the default art for <see cref="TokenType"/>.</summary>
    public string? BackgroundUrl { get; set; }

    public string EffectiveBackgroundUrl => BackgroundUrl
        ?? (TokenType == PlayerType ? "Assets/token.png" : "Assets/reminder.png");

    public string BorderColor { get; set; } = "#1E90FF";

    /// <summary>Colour of the right half of the border; null draws a single-colour ring.</summary>
    public string? BorderColor2 { get; set; }
    public int BorderThickness { get; set; } = 20;

    public bool LeafFirstNight { get; set; }
    public bool LeafOtherNights { get; set; }
    public bool LeafSetup { get; set; }
    public bool LeafReminders { get; set; }
    public int LeafReminderCount { get; set; } = 1;
    public double LeafScale { get; set; } = 1.0;

    public const double DefaultPlayerSizeCm = 5.0;
    public const double DefaultReminderSizeCm = 2.5;

    /// <summary>Printed diameter of player tokens; kept separately so switching type restores each size.</summary>
    public double PlayerSizeCm { get; set; } = DefaultPlayerSizeCm;

    /// <summary>Printed diameter of reminder tokens.</summary>
    public double ReminderSizeCm { get; set; } = DefaultReminderSizeCm;

    /// <summary>Printed diameter of the current <see cref="TokenType"/>.</summary>
    public double SizeCm
    {
        get => IsPlayer ? PlayerSizeCm : ReminderSizeCm;
        set
        {
            if (IsPlayer)
            {
                PlayerSizeCm = value;
            }
            else
            {
                ReminderSizeCm = value;
            }
        }
    }

    /// <summary>Print resolution; with <see cref="SizeCm"/> gives the PNG size in pixels.</summary>
    public int Dpi { get; set; } = 300;

    public int ExportPixels => (int)Math.Round(SizeCm / 2.54 * Dpi);

    public bool IsPlayer => TokenType == PlayerType;

    /// <summary>Fills name, ability, icon and leaf markers from a role (player token).</summary>
    public void ApplyRole(Role role)
    {
        TokenType = PlayerType;
        Text = role.NamePl;
        TextVertical = 0;
        Description = role.AbilityPl;
        ImageUrl = Services.GameDataClient.IconUrl(role.Id);
        LeafFirstNight = role.WakesFirstNight;
        LeafOtherNights = role.WakesOtherNights;
        LeafSetup = role.Setup;

        var reminders = role.RemindersPl.Count + role.RemindersGlobalPl.Count;
        LeafReminders = reminders > 0;
        LeafReminderCount = Math.Clamp(reminders, 1, 5);
    }

    /// <summary>Switches to a reminder token for one of the role's reminder texts.</summary>
    public void ApplyReminder(Role role, string reminder)
    {
        TokenType = ReminderType;
        Text = reminder;

        TextVertical = ReminderTextVertical;
        Description = string.Empty;
        ImageUrl = Services.GameDataClient.IconUrl(role.Id);
    }
}
