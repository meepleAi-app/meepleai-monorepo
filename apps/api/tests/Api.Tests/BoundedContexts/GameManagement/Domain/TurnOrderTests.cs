using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.Tests.Constants;
using Xunit;

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
        Assert.Throws<ArgumentException>(() =>
            new TurnOrder(Guid.NewGuid(), Guid.Empty, new[] { "Alice" }));
    }

    [Fact]
    public void Constructor_WithNullPlayerOrder_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    [Fact]
    public void Constructor_WithEmptyPlayerOrder_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<string>()));
    }

    [Fact]
    public void Constructor_WithValidArgs_SetsInitialState()
    {
        var sessionId = Guid.NewGuid();
        var players = new[] { "Alice", "Bob", "Charlie" };

        var turnOrder = new TurnOrder(Guid.NewGuid(), sessionId, players);

        Assert.Equal(sessionId, turnOrder.SessionId);
        Assert.Equal(players, turnOrder.PlayerOrder);
        Assert.Equal(0, turnOrder.CurrentIndex);
        Assert.Equal(1, turnOrder.RoundNumber);
        Assert.Equal("Alice", turnOrder.CurrentPlayer);
        Assert.Equal("Bob", turnOrder.NextPlayer);
    }

    // ========================================================================
    // Advance
    // ========================================================================

    [Fact]
    public void Advance_FromFirstPlayer_MovesToSecond()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob", "Charlie" });

        var previous = turnOrder.Advance();

        Assert.Equal("Alice", previous);
        Assert.Equal(1, turnOrder.CurrentIndex);
        Assert.Equal("Bob", turnOrder.CurrentPlayer);
        Assert.Equal(1, turnOrder.RoundNumber);
    }

    [Fact]
    public void Advance_FromLastPlayer_WrapsToFirstAndIncrementsRound()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob" });
        turnOrder.Advance(); // Bob is current

        var previous = turnOrder.Advance(); // Wrap

        Assert.Equal("Bob", previous);
        Assert.Equal(0, turnOrder.CurrentIndex);
        Assert.Equal("Alice", turnOrder.CurrentPlayer);
        Assert.Equal(2, turnOrder.RoundNumber);
    }

    [Fact]
    public void Advance_MultipleRounds_IncrementsRoundNumberEachCycle()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob" });

        turnOrder.Advance(); // Alice→Bob (round 1)
        turnOrder.Advance(); // Bob→Alice wrap (round 2)
        turnOrder.Advance(); // Alice→Bob (round 2)
        turnOrder.Advance(); // Bob→Alice wrap (round 3)

        Assert.Equal(3, turnOrder.RoundNumber);
        Assert.Equal("Alice", turnOrder.CurrentPlayer);
    }

    [Fact]
    public void Advance_SinglePlayer_AlwaysIncrementsRound()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice" });

        turnOrder.Advance();

        Assert.Equal(0, turnOrder.CurrentIndex);
        Assert.Equal(2, turnOrder.RoundNumber);
        Assert.Equal("Alice", turnOrder.CurrentPlayer);
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

        Assert.Equal("Alice", turnOrder.NextPlayer);
    }

    // ========================================================================
    // Reorder
    // ========================================================================

    [Fact]
    public void Reorder_WithValidList_ReplacesPlayerOrder()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob" });

        turnOrder.Reorder(new[] { "Bob", "Charlie", "Alice" });

        Assert.Equal(new[] { "Bob", "Charlie", "Alice" }, turnOrder.PlayerOrder);
    }

    [Fact]
    public void Reorder_ClampsCurrentIndexWhenNewListIsShorter()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice", "Bob", "Charlie" });
        turnOrder.Advance(); // index=1 (Bob)
        turnOrder.Advance(); // index=2 (Charlie)

        turnOrder.Reorder(new[] { "Alice", "Bob" }); // shorter: max index 1

        Assert.Equal(1, turnOrder.CurrentIndex);
        Assert.Equal("Bob", turnOrder.CurrentPlayer);
    }

    [Fact]
    public void Reorder_WithNullList_ThrowsArgumentException()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice" });

        Assert.Throws<ArgumentException>(() => turnOrder.Reorder(null!));
    }

    [Fact]
    public void Reorder_WithEmptyList_ThrowsArgumentException()
    {
        var turnOrder = new TurnOrder(Guid.NewGuid(), Guid.NewGuid(), new[] { "Alice" });

        Assert.Throws<ArgumentException>(() => turnOrder.Reorder(Array.Empty<string>()));
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

        Assert.Equal(0, turnOrder.CurrentIndex);
        Assert.Equal(1, turnOrder.RoundNumber);
        Assert.Equal("Alice", turnOrder.CurrentPlayer);
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

        Assert.Equal(id, turnOrder.Id);
        Assert.Equal(sessionId, turnOrder.SessionId);
        Assert.Equal(2, turnOrder.CurrentIndex);
        Assert.Equal(3, turnOrder.RoundNumber);
        Assert.Equal("Charlie", turnOrder.CurrentPlayer);
    }
}
