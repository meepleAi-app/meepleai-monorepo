using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// Tests for the SharedContext value object.
/// Epic #412: Game Toolbox
/// </summary>
[Trait("Category", "Unit")]
public sealed class SharedContextTests
{
    [Fact]
    public void CurrentPlayer_ReturnsCorrectPlayer()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red"), new("Lucia", "blue")],
            CurrentPlayerIndex = 1
        };

        ctx.CurrentPlayer!.Name.Should().Be("Lucia");
        ctx.CurrentPlayer.Color.Should().Be("blue");
    }

    [Fact]
    public void CurrentPlayer_EmptyPlayers_ReturnsNull()
    {
        var ctx = new SharedContext();
        ctx.CurrentPlayer.Should().BeNull();
    }

    [Fact]
    public void CurrentPlayer_IndexOutOfRange_ReturnsNull()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red")],
            CurrentPlayerIndex = 5
        };

        ctx.CurrentPlayer.Should().BeNull();
    }

    [Fact]
    public void AdvancePlayer_CyclesToNext()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red"), new("Lucia", "blue"), new("Leo", "green")],
            CurrentPlayerIndex = 0
        };

        var next = ctx.AdvancePlayer();

        next.CurrentPlayerIndex.Should().Be(1);
        next.CurrentPlayer!.Name.Should().Be("Lucia");
    }

    [Fact]
    public void AdvancePlayer_WrapsAround()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red"), new("Lucia", "blue")],
            CurrentPlayerIndex = 1
        };

        var next = ctx.AdvancePlayer();

        next.CurrentPlayerIndex.Should().Be(0);
    }

    [Fact]
    public void AdvancePlayer_EmptyPlayers_StaysAtZero()
    {
        var ctx = new SharedContext();
        var next = ctx.AdvancePlayer();
        next.CurrentPlayerIndex.Should().Be(0);
    }

    [Fact]
    public void AdvanceRound_IncrementsAndResetsPlayer()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red")],
            CurrentPlayerIndex = 0,
            CurrentRound = 3
        };

        var next = ctx.AdvanceRound();

        next.CurrentRound.Should().Be(4);
        next.CurrentPlayerIndex.Should().Be(0);
    }

    [Fact]
    public void SharedContext_IsImmutable()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red")],
            CurrentPlayerIndex = 0,
            CurrentRound = 1
        };

        var advanced = ctx.AdvancePlayer();

        // Original unchanged
        ctx.CurrentPlayerIndex.Should().Be(0);
        advanced.CurrentPlayerIndex.Should().Be(0); // wraps since 1 player
    }

    [Fact]
    public void SharedContext_CustomProperties()
    {
        var ctx = new SharedContext
        {
            CustomProperties = new Dictionary<string, string>
            {
                ["victoryTarget"] = "10",
                ["maxTurns"] = "30"
            }
        };

        ctx.CustomProperties.Should().HaveCount(2);
        ctx.CustomProperties["victoryTarget"].Should().Be("10");
    }
}
