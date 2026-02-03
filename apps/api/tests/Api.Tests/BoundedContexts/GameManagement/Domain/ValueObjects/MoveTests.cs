using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the Move value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 19
/// </summary>
[Trait("Category", "Unit")]
public sealed class MoveTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesMove()
    {
        // Arrange
        var playerName = "Alice";
        var action = "roll dice";
        var position = "A5";
        var timestamp = DateTime.UtcNow;
        var context = new Dictionary<string, string> { { "diceValue", "6" } };

        // Act
        var move = new Move(playerName, action, position, timestamp, context);

        // Assert
        move.PlayerName.Should().Be("Alice");
        move.Action.Should().Be("roll dice");
        move.Position.Should().Be("A5");
        move.Timestamp.Should().Be(timestamp);
        move.AdditionalContext.Should().ContainKey("diceValue");
    }

    [Fact]
    public void Constructor_WithEmptyPlayerName_ThrowsArgumentException()
    {
        // Act
        var action = () => new Move("", "roll dice");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("playerName")
            .WithMessage("*Player name cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespacePlayerName_ThrowsArgumentException()
    {
        // Act
        var action = () => new Move("   ", "roll dice");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Player name cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyAction_ThrowsArgumentException()
    {
        // Act
        var action = () => new Move("Alice", "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("action")
            .WithMessage("*Action cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceAction_ThrowsArgumentException()
    {
        // Act
        var action = () => new Move("Alice", "   ");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Action cannot be empty*");
    }

    [Fact]
    public void Constructor_TrimsPlayerName()
    {
        // Act
        var move = new Move("  Alice  ", "roll dice");

        // Assert
        move.PlayerName.Should().Be("Alice");
    }

    [Fact]
    public void Constructor_TrimsAction()
    {
        // Act
        var move = new Move("Alice", "  roll dice  ");

        // Assert
        move.Action.Should().Be("roll dice");
    }

    [Fact]
    public void Constructor_TrimsPosition()
    {
        // Act
        var move = new Move("Alice", "move", "  A5  ");

        // Assert
        move.Position.Should().Be("A5");
    }

    [Fact]
    public void Constructor_WithNullPosition_SetsNullPosition()
    {
        // Act
        var move = new Move("Alice", "roll dice", null);

        // Assert
        move.Position.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithWhitespacePosition_SetsNullPosition()
    {
        // Act
        var move = new Move("Alice", "roll dice", "   ");

        // Assert
        move.Position.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithoutTimestamp_DefaultsToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var move = new Move("Alice", "roll dice");

        // Assert
        var after = DateTime.UtcNow;
        move.Timestamp.Should().BeOnOrAfter(before);
        move.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_WithExplicitTimestamp_UsesProvidedValue()
    {
        // Arrange
        var timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var move = new Move("Alice", "roll dice", null, timestamp);

        // Assert
        move.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Constructor_WithNullAdditionalContext_SetsNull()
    {
        // Act
        var move = new Move("Alice", "roll dice", null, null, null);

        // Assert
        move.AdditionalContext.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAdditionalContext_SetsContext()
    {
        // Arrange
        var context = new Dictionary<string, string>
        {
            { "cardName", "Development Card" },
            { "resourceType", "Wood" }
        };

        // Act
        var move = new Move("Alice", "draw card", null, null, context);

        // Assert
        move.AdditionalContext.Should().HaveCount(2);
        move.AdditionalContext!["cardName"].Should().Be("Development Card");
        move.AdditionalContext["resourceType"].Should().Be("Wood");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithPosition_IncludesPosition()
    {
        // Arrange
        var move = new Move("Alice", "move piece", "B3");

        // Act
        var result = move.ToString();

        // Assert
        result.Should().Be("Alice: move piece at B3");
    }

    [Fact]
    public void ToString_WithoutPosition_OmitsPosition()
    {
        // Arrange
        var move = new Move("Alice", "roll dice");

        // Act
        var result = move.ToString();

        // Assert
        result.Should().Be("Alice: roll dice");
    }

    [Fact]
    public void ToString_WithNullPosition_OmitsPosition()
    {
        // Arrange
        var move = new Move("Alice", "draw card", null);

        // Act
        var result = move.ToString();

        // Assert
        result.Should().Be("Alice: draw card");
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var move1 = new Move("Alice", "roll dice", "A5", timestamp);
        var move2 = new Move("Alice", "roll dice", "A5", timestamp);

        // Assert
        move1.Should().Be(move2);
    }

    [Fact]
    public void Equals_WithDifferentPlayerName_ReturnsFalse()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var move1 = new Move("Alice", "roll dice", "A5", timestamp);
        var move2 = new Move("Bob", "roll dice", "A5", timestamp);

        // Assert
        move1.Should().NotBe(move2);
    }

    [Fact]
    public void Equals_WithDifferentAction_ReturnsFalse()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var move1 = new Move("Alice", "roll dice", "A5", timestamp);
        var move2 = new Move("Alice", "draw card", "A5", timestamp);

        // Assert
        move1.Should().NotBe(move2);
    }

    #endregion
}
