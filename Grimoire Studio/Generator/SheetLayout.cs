namespace Grimoire_Studio.Generator;

/// <summary>Lays tokens out on A4 pages in rows, largest first (as the WPF generator's PDF export did).</summary>
public static class SheetLayout
{
    public const double PageWidth = 210;
    public const double PageHeight = 297;
    public const double Margin = 10;
    public const double Gap = 3;

    public static IReadOnlyList<IReadOnlyList<SheetPlacement>> Pack(IEnumerable<SheetItem> items)
    {
        var pages = new List<List<SheetPlacement>>();
        var page = new List<SheetPlacement>();
        double x = Margin, y = Margin, rowHeight = 0;

        // OrderByDescending is stable, so tokens of the same size keep the order they were added in.
        foreach (var item in items.OrderByDescending(i => i.Settings.SizeCm))
        {
            var diameter = item.Settings.SizeCm * 10;

            if (x > Margin && x + diameter > PageWidth - Margin)
            {
                x = Margin;
                y += rowHeight + Gap;
                rowHeight = 0;
            }

            if (y > Margin && y + diameter > PageHeight - Margin)
            {
                pages.Add(page);
                page = [];
                x = Margin;
                y = Margin;
                rowHeight = 0;
            }

            page.Add(new SheetPlacement(item, x, y, diameter));
            x += diameter + Gap;
            rowHeight = Math.Max(rowHeight, diameter);
        }

        if (page.Count > 0)
        {
            pages.Add(page);
        }

        return pages;
    }
}
