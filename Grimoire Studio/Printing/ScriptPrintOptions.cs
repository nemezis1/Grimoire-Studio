namespace Grimoire_Studio.Printing;

/// <summary>Settings of the script printout, remembered in the browser.</summary>
public sealed class ScriptPrintOptions
{
    public const string StorageKey = "grimoire-studio.script-print";

    /// <summary>Bundled calligraphic font of the page titles (wwwroot/Fonts/GreatVibes.ttf).</summary>
    public const string TitleFont = "Great Vibes";

    /// <summary>1 or 2 columns of roles on the character sheet.</summary>
    public int Columns { get; set; } = 2;

    /// <summary>Text size relative to the default; content is still shrunk to fit its page.</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>Ability in its own column beside a slightly larger icon and name (in one or two columns of roles).</summary>
    public bool AbilityBesideName { get; set; }

    public bool ShowPlayerCount { get; set; } = true;

    /// <summary>Second page with travellers, fabled and jinxes (when the script has any).</summary>
    public bool IncludeExtras { get; set; } = true;

    public bool IncludeNightOrder { get; set; } = true;

    public bool NightIncludesTravellers { get; set; } = true;

    /// <summary>Storyteller instructions (night reminders) under each step of the night order.</summary>
    public bool NightShowsReminder { get; set; } = true;

    /// <summary>Role ability under the name in the night order.</summary>
    public bool NightShowsAbility { get; set; }

    /// <summary>Minion and demon information steps on the first night.</summary>
    public bool NightShowsEvilInfo { get; set; } = true;

    /// <summary>Other nights printed upside down, so both halves read correctly from opposite sides of a table.</summary>
    public bool FlipOtherNights { get; set; }

    /// <summary>Size of the pages on screen only; printing is always 1:1.</summary>
    public double PreviewZoom { get; set; } = 0.9;
}
