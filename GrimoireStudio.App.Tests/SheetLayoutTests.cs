using Grimoire_Studio.Generator;

namespace GrimoireStudio.App.Tests;

public class SheetLayoutTests
{
    private static SheetItem Token(double sizeCm, bool player = true) =>
        new(new TokenSettings
        {
            TokenType = player ? TokenSettings.PlayerType : TokenSettings.ReminderType,
            PlayerSizeCm = sizeCm,
            ReminderSizeCm = sizeCm,
        }, null);

    private static IReadOnlyList<IReadOnlyList<SheetPlacement>> Pack(params SheetItem[] items) => SheetLayout.Pack(items);

    [Fact]
    public void Pack_Should_FillWholeRowsWithSmallTokens_When_BelowARowOfBigOnes()
    {
        // Three 5 cm player tokens and twenty 2.5 cm reminders.
        var items = Enumerable.Range(0, 3).Select(_ => Token(5))
            .Concat(Enumerable.Range(0, 20).Select(_ => Token(2.5, player: false)))
            .ToArray();

        var page = Assert.Single(Pack(items));
        var small = page.Where(p => p.Diameter == 25).ToList();

        // Down to two reminder rows below the players (y < 119 mm): the strip on the right takes four, and the rows under
        // the players five each when they span the page width; one per player column (the old packing) gave only ten.
        Assert.True(small.Count(p => p.Y < 119) >= 14, string.Join(", ", small.Select(p => $"({p.X}, {p.Y})")));
    }

    [Fact]
    public void Pack_Should_NeverOverlapOrLeaveThePage_When_SizesAreMixed()
    {
        var sizes = new[] { 5, 5, 5, 5, 4, 4, 3, 2.5, 2.5, 2.5, 2.5, 2.5, 2.5, 2.5, 2, 2, 2, 2 };
        var pages = Pack(Enumerable.Range(0, 4).SelectMany(_ => sizes).Select(s => Token(s)).ToArray());

        foreach (var page in pages)
        {
            foreach (var a in page)
            {
                Assert.True(a.X >= SheetLayout.Margin - 0.01 && a.Y >= SheetLayout.Margin - 0.01);
                Assert.True(a.X + a.Diameter <= SheetLayout.PageWidth - SheetLayout.Margin + 0.01);
                Assert.True(a.Y + a.Diameter <= SheetLayout.PageHeight - SheetLayout.Margin + 0.01);
                foreach (var b in page.Where(b => !ReferenceEquals(a, b)))
                {
                    var apart = a.X + a.Diameter + SheetLayout.Gap <= b.X + 0.01 || b.X + b.Diameter + SheetLayout.Gap <= a.X + 0.01
                        || a.Y + a.Diameter + SheetLayout.Gap <= b.Y + 0.01 || b.Y + b.Diameter + SheetLayout.Gap <= a.Y + 0.01;
                    Assert.True(apart, $"Tokens at ({a.X}, {a.Y}) and ({b.X}, {b.Y}) overlap.");
                }
            }
        }
    }

    [Fact]
    public void Pack_Should_PutRemindersBesideBigTokens_When_TheStripFits()
    {
        // Three 5 cm tokens take 159 mm of the 190; a 2.5 cm reminder fits in the strip to their right.
        var page = Assert.Single(Pack(Token(5), Token(5), Token(5), Token(2.5, player: false)));
        var reminder = page.Single(p => p.Diameter == 25);
        Assert.Equal(10, reminder.Y, 3);
    }
}
