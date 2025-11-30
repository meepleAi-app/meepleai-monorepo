using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class MoveTests
{
    #region Constructor Tests

    [Fact]
    public void Move_WithValidParameters_CreatesSuccessfully()
    {
        // Arrange & Act
        var move = new Move("Alice", "roll dice");

        // Assert
        Assert.Equal("Alice", move.PlayerName);
        Assert.Equal("roll dice", move.Action);
        Assert.Null(move.Position);
        Assert.NotEqual(default, move.Timestamp);
        Assert.Null(move.AdditionalContext);
    }

    [Fact]
    public void Move_WithAllParameters_CreatesSuccessfully()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var context = new Dictionary<string, string>
        {
            { "diceValue", "6" },
            { "color", "red" }
        };

        // Act
        var move = new Move("Bob", "move piece", "A5", timestamp, context);

        // Assert
        Assert.Equal("Bob", move.PlayerName);
        Assert.Equal("move piece", move.Action);
        Assert.Equal("A5", move.Position);
        Assert.Equal(timestamp, move.Timestamp);
        Assert.Equal(2, move.AdditionalContext!.Count);
        Assert.Equal("6", move.AdditionalContext["diceValue"]);
    }

    [Fact]
    public void Move_TrimsWhitespace_FromPlayerNameAndAction()
    {
        // Arrange & Act
        var move = new Move("  Alice  ", "  roll dice  ", "  A1  ");

        // Assert
        Assert.Equal("Alice", move.PlayerName);
        Assert.Equal("roll dice", move.Action);
        Assert.Equal("A1", move.Position);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Move_WithInvalidPlayerName_ThrowsArgumentException(string? playerName)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new Move(playerName!, "roll dice"));
        Assert.Contains("Player name cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Move_WithInvalidAction_ThrowsArgumentException(string? action)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new Move("Alice", action!));
        Assert.Contains("Action cannot be empty", exception.Message);
    }

    [Fact]
    public void Move_WithNullPosition_SetsPositionToNull()
    {
        // Arrange & Act
        var move = new Move("Alice", "draw card", position: null);

        // Assert
        Assert.Null(move.Position);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Move_WithEmptyPosition_SetsPositionToNull(string position)
    {
        // Arrange & Act
        var move = new Move("Alice", "draw card", position);

        // Assert
        Assert.Null(move.Position);
    }

    [Fact]
    public void Move_WithoutTimestamp_UsesCurrentTime()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var move = new Move("Alice", "roll dice");

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(move.Timestamp, before, after.AddSeconds(1));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithoutPosition_ReturnsFormattedString()
    {
        // Arrange
        var move = new Move("Alice", "roll dice");

        // Act
        var result = move.ToString();

        // Assert
        Assert.Equal("Alice: roll dice", result);
    }

    [Fact]
    public void ToString_WithPosition_ReturnsFormattedStringWithPosition()
    {
        // Arrange
        var move = new Move("Bob", "move piece", "A5");

        // Act
        var result = move.ToString();

        // Assert
        Assert.Equal("Bob: move piece at A5", result);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Move_EqualityByValue_WorksCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var context = new Dictionary<string, string> { { "key", "value" } };

        var move1 = new Move("Alice", "roll dice", "A1", timestamp, context);
        var move2 = new Move("Alice", "roll dice", "A1", timestamp, context);

        // Act & Assert
        Assert.Equal(move1, move2);
        Assert.True(move1 == move2);
    }

    [Fact]
    public void Move_DifferentPlayerName_AreNotEqual()
    {
        // Arrange
        var move1 = new Move("Alice", "roll dice");
        var move2 = new Move("Bob", "roll dice");

        // Act & Assert
        Assert.NotEqual(move1, move2);
    }

    [Fact]
    public void Move_DifferentAction_AreNotEqual()
    {
        // Arrange
        var move1 = new Move("Alice", "roll dice");
        var move2 = new Move("Alice", "draw card");

        // Act & Assert
        Assert.NotEqual(move1, move2);
    }

    #endregion
}

