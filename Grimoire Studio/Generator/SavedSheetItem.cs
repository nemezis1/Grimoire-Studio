namespace Grimoire_Studio.Generator;

/// <summary>Stored form of a <see cref="SheetItem"/>; the rendered image is recreated after loading.</summary>
public sealed record SavedSheetItem(string? RoleId, TokenSettings Settings);
