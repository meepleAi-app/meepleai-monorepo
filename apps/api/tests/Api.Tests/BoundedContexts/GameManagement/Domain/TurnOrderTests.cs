using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

/// <summary>
/// Unit tests for the TurnOrder domain entity.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class TurnOrderTests
{
    // ========================================================================
    // Constructor guards
    // ========================================================================

    [Fact]
    public void Constructor_WithEmptySessionId_ThrowsArgumentException()
    {
        var act = () =>
            new TurnOrder(Guid.NewGuid(), Guid.Empty, new[] { "Alice" });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullPlayerOrder_ThrowsArgumentException()
    {
        var act = () =>
            new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyPlayerOrder_ThrowsArgumentException()
    {
        var act = () =>
            new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<string>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidArgs_SetsInitialState()
    {
        var sessionId = Guid.NewGuid();
        var players = new[] { "Alice", "Bob", "Charlie" };

        var turnOrder = new TurnOrder(Guid.NewGuid(), sessionId, players);

        turnOrder.SessionId.Should().Be(sessionId);
        turnOrder.PlayerOrder.Should().Equal(players);
        turnOrder.CurrentIndex.Should().Be(0);
        turnOrder.RoundNumber.Should().Be(1);
        turnOrder.CurrentPlayer.Should().Be("Alice");
        turnOrder.NextPlayer.Should().Be("Bob");
    }

    // ========================================================================
    // Advance
    // ========================================================================

    [Fact]
    public void Advance_FromFirstPlayer_MovesToSecond()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob", "Charlie" });

        var previous = turnOrder.Advance();

        previous.Should().Be("Alice");
        turnOrder.CurrentIndex.Should().Be(1);
        turnOrder.CurrentPlayer.Should().Be("Bob");
        turnOrder.RoundNumber.Should().Be(1);
    }

    [Fact]
    public void Advance_FromLastPlayer_WrapsToFirstAndIncrementsRound()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob" });
        turnOrder.Advance(); // Bob is current

        var previous = turnOrder.Advance(); // Wrap

        previous.Should().Be("Bob");
        turnOrder.CurrentIndex.Should().Be(0);
        turnOrder.CurrentPlayer.Should().Be("Alice");
        turnOrder.RoundNumber.Should().Be(2);
    }

    [Fact]
    public void Advance_MultipleRounds_IncrementsRoundNumberEachCycle()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob" });

        turnOrder.Advance(); // Alice→Bob (round 1)
        turnOrder.Advance(); // Bob→Alice wrap (round 2)
        turnOrder.Advance(); // Alice→Bob (round 2)
        turnOrder.Advance(); // Bob→Alice wrap (round 3)

        turnOrder.RoundNumber.Should().Be(3);
        turnOrder.CurrentPlayer.Should().Be("Alice");
    }

    [Fact]
    public void Advance_SinglePlayer_AlwaysIncrementsRound()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice" });

        turnOrder.Advance();

        turnOrder.CurrentIndex.Should().Be(0);
        turnOrder.RoundNumber.Should().Be(2);
        turnOrder.CurrentPlayer.Should().Be("Alice");
    }

    // ========================================================================
    // NextPlayer
    // ========================================================================

    [Fact]
    public void NextPlayer_WhenCurrentIsLast_ReturnsFirst()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob", "Charlie" });
        turnOrder.Advance(); // Bob
        turnOrder.Advance(); // Charlie is current

        turnOrder.NextPlayer.Should().Be("Alice");
    }

    // ========================================================================
    // Reorder
    // ========================================================================

    [Fact]
    public void Reorder_WithValidList_ReplacesPlayerOrder()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob" });

        turnOrder.Reorder(new[] { "Bob", "Charlie", "Alice" });

        turnOrder.PlayerOrder.Should().BeEquivalentTo(new[] { "Bob", "Charlie", "Alice" });
    }

    [Fact]
    public void Reorder_ClampsCurrentIndexWhenNewListIsShorter()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob", "Charlie" });
        turnOrder.Advance(); // index=1 (Bob)
        turnOrder.Advance(); // index=2 (Charlie)

        turnOrder.Reorder(new[] { "Alice", "Bob" }); // shorter: max index 1

        turnOrder.CurrentIndex.Should().Be(1);
        turnOrder.CurrentPlayer.Should().Be("Bob");
    }

    [Fact]
    public void Reorder_WithNullList_ThrowsArgumentException()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice" });

        var act = () => turnOrder.Reorder(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reorder_WithEmptyList_ThrowsArgumentException()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice" });

        var act = () => turnOrder.Reorder(Array.Empty<string>());
        act.Should().Throw<ArgumentException>();
    }

    // ========================================================================
    // Reset
    // ========================================================================

    [Fact]
    public void Reset_AfterAdvancing_ResetsToFirstPlayerAndRoundOne()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob", "Charlie" });
        turnOrder.Advance();
        turnOrder.Advance();
        turnOrder.Advance(); // round 2

        turnOrder.Reset();

        turnOrder.CurrentIndex.Should().Be(0);
        turnOrder.RoundNumber.Should().Be(1);
        turnOrder.CurrentPlayer.Should().Be("Alice");
    }

    // ========================================================================
    // Restore factory
    // ========================================================================

    [Fact]
    public void Restore_SetsAllFieldsCorrectly()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var players = new List<string> { "Alice", "Bob", "Charlie" };
        var now = DateTime.UtcNow;

        var turnOrder = TurnOrder.Restore(id, sessionId, players, 2, 3, now, now);

        turnOrder.Id.Should().Be(id);
        turnOrder.SessionId.Should().Be(sessionId);
        turnOrder.CurrentIndex.Should().Be(2);
        turnOrder.RoundNumber.Should().Be(3);
        turnOrder.CurrentPlayer.Should().Be("Charlie");
    }
}
