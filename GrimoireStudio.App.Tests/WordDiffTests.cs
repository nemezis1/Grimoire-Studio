using Grimoire_Studio.Services;

namespace GrimoireStudio.App.Tests;

public class WordDiffTests
{
    [Fact]
    public void Compare_Should_MarkTheChangedWordOnly_When_OneWordDiffers()
    {
        var runs = WordDiff.Compare("Each night, you learn how many of your 2 alive neighbours are evil.", "Each night, you learn how many of your 2 alive neighbors are evil.");

        Assert.Equal(
            [
                ("Each night, you learn how many of your 2 alive ", WordDiff.Kind.Same),
                ("neighbours ", WordDiff.Kind.Removed),
                ("neighbors ", WordDiff.Kind.Added),
                ("are evil.", WordDiff.Kind.Same),
            ],
            runs);
    }
}
