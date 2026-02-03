using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the SessionPlayer value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 17
/// </summary>
[Trait("Category", "Unit")]
public sealed class SessionPlayerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesSessionPlayer()
    {
        // Arrange
        var playerName = "Alice";
        var playerOrder = 1;
        var color = "Red";

        // Act
        var player = new SessionPlayer(playerName, playerOrder, color);

        // Assert
        player.PlayerName.Should().Be(playerName);
        player.PlayerOrder.Should().Be(playerOrder);
        player.Color.Should().Be(color);
    }

    [Fact]
    public void Constructor_WithoutColor_CreatesPlayerWithNullColor()
    {
        // Act
        var player = new SessionPlayer("Alice", 1);

        // Assert
        player.Color.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsValidationException()
    {
        // Act
        var action = () => new SessionPlayer("", 1);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player name cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsValidationException()
    {
        // Act
        var action = () => new SessionPlayer("   ", 1);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player name cannot be empty*");
    }

    [Fact]
    public void Constructor_WithNameExceeding50Characters_ThrowsValidationException()
    {
        // Arrange
        var longName = new string('a', 51);

        // Act
        var action = () => new SessionPlayer(longName, 1);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player name cannot exceed 50 characters*");
    }

    [Fact]
    public void Constructor_WithNameExactly50Characters_Succeeds()
    {
        // Arrange
        var maxName = new string('a', 50);

        // Act
        var player = new SessionPlayer(maxName, 1);

        // Assert
        player.PlayerName.Should().HaveLength(50);
    }

    [Fact]
    public void Constructor_WithPlayerOrderLessThan1_ThrowsValidationException()
    {
        // Act
        var action = () => new SessionPlayer("Alice", 0);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player order must be at least 1*");
    }

    [Fact]
    public void Constructor_WithNegativePlayerOrder_ThrowsValidationException()
    {
        // Act
        var action = () => new SessionPlayer("Alice", -1);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player order must be at least 1*");
    }

    [Fact]
    public void Constructor_WithPlayerOrderExceeding100_ThrowsValidationException()
    {
        // Act
        var action = () => new SessionPlayer("Alice", 101);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player order cannot exceed 100*");
    }

    [Fact]
    public void Constructor_WithPlayerOrderExactly100_Succeeds()
    {
        // Act
        var player = new SessionPlayer("Alice", 100);

        // Assert
        player.PlayerOrder.Should().Be(100);
    }

    [Fact]
    public void Constructor_WithPlayerOrderExactly1_Succeeds()
    {
        // Act
        var player = new SessionPlayer("Alice", 1);

        // Assert
        player.PlayerOrder.Should().Be(1);
    }

    [Fact]
    public void Constructor_TrimsPlayerName()
    {
        // Act
        var player = new SessionPlayer("  Alice  ", 1);

        // Assert
        player.PlayerName.Should().Be("Alice");
    }

    [Fact]
    public void Constructor_TrimsColor()
    {
        // Act
        var player = new SessionPlayer("Alice", 1, "  Red  ");

        // Assert
        player.Color.Should().Be("Red");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameNameAndOrder_ReturnsTrue()
    {
        // Arrange
        var player1 = new SessionPlayer("Alice", 1, "Red");
        var player2 = new SessionPlayer("Alice", 1, "Blue"); // Different color

        // Act & Assert
        player1.Should().Be(player2);
    }

    [Fact]
    public void Equals_WithDifferentNameCase_ReturnsTrue()
    {
        // Arrange
        var player1 = new SessionPlayer("Alice", 1);
        var player2 = new SessionPlayer("ALICE", 1);

        // Act & Assert
        player1.Should().Be(player2);
    }

    [Fact]
    public void Equals_WithDifferentName_ReturnsFalse()
    {
        // Arrange
        var player1 = new SessionPlayer("Alice", 1);
        var player2 = new SessionPlayer("Bob", 1);

        // Act & Assert
        player1.Should().NotBe(player2);
    }

    [Fact]
    public void Equals_WithDifferentOrder_ReturnsFalse()
    {
        // Arrange
        var player1 = new SessionPlayer("Alice", 1);
        var player2 = new SessionPlayer("Alice", 2);

        // Act & Assert
        player1.Should().NotBe(player2);
    }

    [Fact]
    public void GetHashCode_WithSameNameAndOrder_ReturnsSameHash()
    {
        // Arrange
        var player1 = new SessionPlayer("Alice", 1);
        var player2 = new SessionPlayer("Alice", 1);

        // Act & Assert
        player1.GetHashCode().Should().Be(player2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithColor_IncludesColor()
    {
        // Arrange
        var player = new SessionPlayer("Alice", 1, "Red");

        // Act
        var result = player.ToString();

        // Assert
        result.Should().Be("Alice (Red, P1)");
    }

    [Fact]
    public void ToString_WithoutColor_OmitsColor()
    {
        // Arrange
        var player = new SessionPlayer("Alice", 1);

        // Act
        var result = player.ToString();

        // Assert
        result.Should().Be("Alice (P1)");
    }

    #endregion
}
