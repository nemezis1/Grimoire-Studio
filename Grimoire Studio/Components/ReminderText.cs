namespace Grimoire_Studio.Components;

/// <summary>One reminder token of a role being edited: its Polish name and its English one (empty: same as Polish).</summary>
public sealed class ReminderText
{
    public string Pl { get; set; } = string.Empty;
    public string En { get; set; } = string.Empty;

    /// <summary>Pairs the Polish and English lists by position.</summary>
    public static List<ReminderText> From(IReadOnlyList<string> pl, IReadOnlyList<string> en) =>
        pl.Select((name, i) => new ReminderText { Pl = name, En = i < en.Count && en[i] != name ? en[i] : string.Empty }).ToList();

    /// <summary>The Polish names, without empty tiles.</summary>
    public static List<string> Polish(IEnumerable<ReminderText> items) =>
        items.Where(i => !string.IsNullOrWhiteSpace(i.Pl)).Select(i => i.Pl.Trim()).ToList();

    /// <summary>The English names in the same order; an empty one takes the Polish name.</summary>
    public static List<string> English(IEnumerable<ReminderText> items) =>
        items.Where(i => !string.IsNullOrWhiteSpace(i.Pl))
            .Select(i => string.IsNullOrWhiteSpace(i.En) ? i.Pl.Trim() : i.En.Trim())
            .ToList();
}
