namespace Grimoire_Studio.Generator;

/// <summary>
/// Lays tokens out on A4 pages, largest first. Each page keeps a list of free rectangles; a token goes into the
/// topmost-leftmost one it fits on any page, so smaller tokens fill the space left beside and below bigger ones
/// (e.g. reminders in the strip to the right of three player tokens).
/// </summary>
public static class SheetLayout
{
    public const double PageWidth = 210;
    public const double PageHeight = 297;
    public const double Margin = 10;
    public const double Gap = 3;

    // A fraction of a millimetre is rounding, not overflow.
    private const double Tolerance = 0.01;

    public static IReadOnlyList<IReadOnlyList<SheetPlacement>> Pack(IEnumerable<SheetItem> items)
    {
        var pages = new List<(List<SheetPlacement> Placements, List<Free> Free)>();

        // OrderByDescending is stable, so tokens of the same size keep the order they were added in.
        foreach (var item in items.OrderByDescending(i => i.Settings.SizeCm))
        {
            var diameter = item.Settings.SizeCm * 10;
            var spot = pages
                .Select((page, index) => (Index: index, Free: page.Free
                    .Where(f => f.Fits(diameter))
                    .OrderBy(f => f.Y)
                    .ThenBy(f => f.X)
                    .FirstOrDefault()))
                .FirstOrDefault(p => p.Free is not null);

            if (spot.Free is null)
            {
                pages.Add(([], [new Free(Margin, Margin, PageWidth - 2 * Margin, PageHeight - 2 * Margin)]));
                spot = (pages.Count - 1, pages[^1].Free[0]);
            }

            var (placements, free) = pages[spot.Index];
            var rect = spot.Free!;
            placements.Add(new SheetPlacement(item, rect.X, rect.Y, diameter));

            // Guillotine split: the strip to the right keeps the full height, the space below the token its width.
            free.Remove(rect);
            free.Add(new Free(rect.X + diameter + Gap, rect.Y, rect.Width - diameter - Gap, rect.Height));
            free.Add(new Free(rect.X, rect.Y + diameter + Gap, diameter, rect.Height - diameter - Gap));
            free.RemoveAll(f => f.Width <= 0 || f.Height <= 0);
        }

        return pages.Select(p => (IReadOnlyList<SheetPlacement>)p.Placements).ToList();
    }

    private sealed record Free(double X, double Y, double Width, double Height)
    {
        public bool Fits(double diameter) => diameter <= Width + Tolerance && diameter <= Height + Tolerance;
    }
}
