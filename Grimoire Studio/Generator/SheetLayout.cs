namespace Grimoire_Studio.Generator;

/// <summary>
/// Lays tokens out on A4 pages, largest first. Each page keeps every free rectangle as large as it can be (they may
/// overlap); a token goes into the topmost-leftmost spot it fits on any page. Space left beside and below bigger
/// tokens therefore stays usable across their columns: smaller tokens fill whole rows under a row of player tokens,
/// not one narrow column under each.
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
        var pages = new List<(List<SheetPlacement> Placements, List<Rect> Free)>();

        // OrderByDescending is stable, so tokens of the same size keep the order they were added in.
        foreach (var item in items.OrderByDescending(i => i.Settings.SizeCm))
        {
            var diameter = item.Settings.SizeCm * 10;

            // Each token takes its square plus the gap to its right and below; the page area gets the same extra
            // gap, so a token may end exactly at the margin.
            var size = diameter + Gap;
            var spot = pages
                .Select((page, index) => (Index: index, Free: page.Free
                    .Where(f => f.Fits(size))
                    .OrderBy(f => Math.Round(f.Y, 3))
                    .ThenBy(f => f.X)
                    .FirstOrDefault()))
                .FirstOrDefault(p => p.Free is not null);

            if (spot.Free is null)
            {
                pages.Add(([], [new Rect(Margin, Margin, PageWidth - 2 * Margin + Gap, PageHeight - 2 * Margin + Gap)]));
                spot = (pages.Count - 1, pages[^1].Free[0]);
            }

            var (placements, free) = pages[spot.Index];
            var taken = new Rect(spot.Free!.X, spot.Free.Y, size, size);
            placements.Add(new SheetPlacement(item, taken.X, taken.Y, diameter));
            Take(free, taken);
        }

        return pages.Select(p => (IReadOnlyList<SheetPlacement>)p.Placements).ToList();
    }

    /// <summary>
    /// Removes <paramref name="taken"/> from the free rectangles: each one it overlaps is replaced by the up to four
    /// largest rectangles left around it, and rectangles inside others are dropped.
    /// </summary>
    private static void Take(List<Rect> free, Rect taken)
    {
        var pieces = new List<Rect>();
        foreach (var rect in free.ToList())
        {
            if (!rect.Overlaps(taken))
            {
                continue;
            }

            free.Remove(rect);
            pieces.Add(rect with { Width = taken.X - rect.X });
            pieces.Add(rect with { X = taken.Right, Width = rect.Right - taken.Right });
            pieces.Add(rect with { Height = taken.Y - rect.Y });
            pieces.Add(rect with { Y = taken.Bottom, Height = rect.Bottom - taken.Bottom });
        }

        var kept = new List<Rect>();
        foreach (var rect in free.Concat(pieces).Where(p => p.Width > Tolerance && p.Height > Tolerance))
        {
            if (kept.Any(k => k.Contains(rect)))
            {
                continue;
            }

            kept.RemoveAll(rect.Contains);
            kept.Add(rect);
        }

        free.Clear();
        free.AddRange(kept);
    }

    private sealed record Rect(double X, double Y, double Width, double Height)
    {
        public double Right => X + Width;

        public double Bottom => Y + Height;

        public bool Fits(double size) => size <= Width + Tolerance && size <= Height + Tolerance;

        public bool Overlaps(Rect other) =>
            X < other.Right - Tolerance && other.X < Right - Tolerance && Y < other.Bottom - Tolerance && other.Y < Bottom - Tolerance;

        public bool Contains(Rect other) =>
            other.X >= X - Tolerance && other.Y >= Y - Tolerance && other.Right <= Right + Tolerance && other.Bottom <= Bottom + Tolerance;
    }
}
