namespace Grimoire_Studio.Generator;

/// <summary>Position of a token on an A4 page, in millimetres from the top-left corner.</summary>
public sealed record SheetPlacement(SheetItem Item, double X, double Y, double Diameter);
