namespace Grimoire_Studio.Services;

/// <summary>One field of a change: its value before and after; null Before means the item is new.</summary>
public sealed record FieldChange(string Label, string? Before, string After, bool IsImage = false);
