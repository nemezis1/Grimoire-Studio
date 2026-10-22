using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class PlayerCountTests
{
    [Fact]
    public void Table_Should_CoverEveryPlayerCountOnce()
    {
        Assert.Equal(
            Enumerable.Range(PlayerCount.MinPlayers, PlayerCount.MaxPlayers - PlayerCount.MinPlayers + 1),
            PlayerCount.Table.Select(c => c.Players));
    }

    [Fact]
    public void Table_Should_AddUpToPlayers()
    {
        Assert.All(PlayerCount.Table, c => Assert.Equal(c.Players, c.Townsfolk + c.Outsiders + c.Minions + c.Demons));
    }
}
