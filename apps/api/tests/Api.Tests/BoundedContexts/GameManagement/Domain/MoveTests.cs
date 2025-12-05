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

    [Theory]
    [InlineData("Alice", "roll dice", null, "Alice: roll dice")]
    [InlineData("Bob", "move piece", "A5", "Bob: move piece at A5")]
    public void ToString_ReturnsFormattedString(string playerName, string action, string? position, string expected)
    {
        // Arrange
        var move = position == null 
            ? new Move(playerName, action) 
            : new Move(playerName, action, position);

        // Act
        var result = move.ToString();

        // Assert
        Assert.Equal(expected, result);
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

    [Theory]
    [InlineData("Alice", "roll dice", "Bob", "roll dice")]      // Different player
    [InlineData("Alice", "roll dice", "Alice", "draw card")]    // Different action
    public void Move_DifferentProperties_AreNotEqual(string player1, string action1, string player2, string action2)
    {
        // Arrange
        var move1 = new Move(player1, action1);
        var move2 = new Move(player2, action2);

        // Act & Assert
        Assert.NotEqual(move1, move2);
    }

    #endregion
}