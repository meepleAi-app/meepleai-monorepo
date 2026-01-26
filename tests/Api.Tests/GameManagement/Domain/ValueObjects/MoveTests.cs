using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.GameManagement.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class MoveTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesMove()
    {
        // Arrange
        var playerName = "John";
        var action = "roll dice";

        // Act
        var move = new Move(playerName, action);

        // Assert
        move.PlayerName.Should().Be("John");
        move.Action.Should().Be("roll dice");
        move.Position.Should().BeNull();
        move.AdditionalContext.Should().BeNull();
        move.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithAllParameters_CreatesMove()
    {
        // Arrange
        var playerName = "Alice";
        var action = "move piece";
        var position = "A5";
        var timestamp = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var context = new Dictionary<string, string> { { "piece", "knight" } };

        // Act
        var move = new Move(playerName, action, position, timestamp, context);

        // Assert
        move.PlayerName.Should().Be("Alice");
        move.Action.Should().Be("move piece");
        move.Position.Should().Be("A5");
        move.Timestamp.Should().Be(timestamp);
        move.AdditionalContext.Should().ContainKey("piece").And.ContainValue("knight");
    }

    [Fact]
    public void Constructor_TrimsPlayerName()
    {
        // Arrange & Act
        var move = new Move("  Bob  ", "take card");

        // Assert
        move.PlayerName.Should().Be("Bob");
    }

    [Fact]
    public void Constructor_TrimsAction()
    {
        // Arrange & Act
        var move = new Move("Bob", "  draw card  ");

        // Assert
        move.Action.Should().Be("draw card");
    }

    [Fact]
    public void Constructor_TrimsPosition()
    {
        // Arrange & Act
        var move = new Move("Bob", "place token", "  B3  ");

        // Assert
        move.Position.Should().Be("B3");
    }

    [Fact]
    public void Constructor_WithEmptyPosition_SetsPositionToNull()
    {
        // Arrange & Act
        var move = new Move("Bob", "action", "   ");

        // Assert
        move.Position.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidPlayerName_ThrowsArgumentException(string? invalidName)
    {
        // Act & Assert
        var action = () => new Move(invalidName!, "action");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Player name cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidAction_ThrowsArgumentException(string? invalidAction)
    {
        // Act & Assert
        var action = () => new Move("Player", invalidAction!);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Action cannot be empty*");
    }

    [Fact]
    public void Constructor_WithNullTimestamp_UsesCurrentTime()
    {
        // Arrange & Act
        var before = DateTime.UtcNow;
        var move = new Move("Player", "action", null, null);
        var after = DateTime.UtcNow;

        // Assert
        move.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
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
        result.Should().Be("Alice: roll dice");
    }

    [Fact]
    public void ToString_WithPosition_IncludesPosition()
    {
        // Arrange
        var move = new Move("Bob", "move piece", "C4");

        // Act
        var result = move.ToString();

        // Assert
        result.Should().Be("Bob: move piece at C4");
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equals_TwoIdenticalMoves_AreEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var move1 = new Move("Player1", "action", "A1", timestamp);
        var move2 = new Move("Player1", "action", "A1", timestamp);

        // Act & Assert
        move1.Should().Be(move2);
    }

    [Fact]
    public void Equals_MovesDifferingByPlayerName_AreNotEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var move1 = new Move("Player1", "action", "A1", timestamp);
        var move2 = new Move("Player2", "action", "A1", timestamp);

        // Act & Assert
        move1.Should().NotBe(move2);
    }

    [Fact]
    public void Equals_MovesDifferingByAction_AreNotEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var move1 = new Move("Player1", "action1", "A1", timestamp);
        var move2 = new Move("Player1", "action2", "A1", timestamp);

        // Act & Assert
        move1.Should().NotBe(move2);
    }

    [Fact]
    public void Equals_MovesDifferingByTimestamp_AreNotEqual()
    {
        // Arrange
        var move1 = new Move("Player1", "action", "A1", DateTime.UtcNow);
        var move2 = new Move("Player1", "action", "A1", DateTime.UtcNow.AddMinutes(1));

        // Act & Assert
        move1.Should().NotBe(move2);
    }

    #endregion

    #region Additional Context Tests

    [Fact]
    public void Constructor_WithAdditionalContext_PreservesContext()
    {
        // Arrange
        var context = new Dictionary<string, string>
        {
            { "card", "ace of spades" },
            { "value", "high" }
        };

        // Act
        var move = new Move("Player", "play card", additionalContext: context);

        // Assert
        move.AdditionalContext.Should().HaveCount(2);
        move.AdditionalContext!["card"].Should().Be("ace of spades");
        move.AdditionalContext["value"].Should().Be("high");
    }

    [Fact]
    public void Constructor_WithEmptyAdditionalContext_PreservesEmptyDictionary()
    {
        // Arrange
        var context = new Dictionary<string, string>();

        // Act
        var move = new Move("Player", "action", additionalContext: context);

        // Assert
        move.AdditionalContext.Should().NotBeNull().And.BeEmpty();
    }

    #endregion
}
