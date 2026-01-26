using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.GameManagement.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class SessionPlayerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesPlayer()
    {
        // Arrange & Act
        var player = new SessionPlayer("John", 1);

        // Assert
        player.PlayerName.Should().Be("John");
        player.PlayerOrder.Should().Be(1);
        player.Color.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithColor_SetsColor()
    {
        // Arrange & Act
        var player = new SessionPlayer("Alice", 2, "Red");

        // Assert
        player.Color.Should().Be("Red");
    }

    [Fact]
    public void Constructor_TrimsPlayerName()
    {
        // Arrange & Act
        var player = new SessionPlayer("  Bob  ", 1);

        // Assert
        player.PlayerName.Should().Be("Bob");
    }

    [Fact]
    public void Constructor_TrimsColor()
    {
        // Arrange & Act
        var player = new SessionPlayer("Bob", 1, "  Blue  ");

        // Assert
        player.Color.Should().Be("Blue");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidPlayerName_ThrowsValidationException(string? invalidName)
    {
        // Act & Assert
        var action = () => new SessionPlayer(invalidName!, 1);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player name cannot be empty*");
    }

    [Fact]
    public void Constructor_WithTooLongPlayerName_ThrowsValidationException()
    {
        // Arrange
        var longName = new string('A', 51);

        // Act & Assert
        var action = () => new SessionPlayer(longName, 1);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player name cannot exceed 50 characters*");
    }

    [Fact]
    public void Constructor_WithMaxLengthPlayerName_Succeeds()
    {
        // Arrange
        var maxName = new string('A', 50);

        // Act
        var player = new SessionPlayer(maxName, 1);

        // Assert
        player.PlayerName.Should().HaveLength(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidPlayerOrder_ThrowsValidationException(int invalidOrder)
    {
        // Act & Assert
        var action = () => new SessionPlayer("Player", invalidOrder);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player order must be at least 1*");
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    public void Constructor_WithTooHighPlayerOrder_ThrowsValidationException(int invalidOrder)
    {
        // Act & Assert
        var action = () => new SessionPlayer("Player", invalidOrder);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Player order cannot exceed 100*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Constructor_WithValidPlayerOrder_Succeeds(int validOrder)
    {
        // Act
        var player = new SessionPlayer("Player", validOrder);

        // Assert
        player.PlayerOrder.Should().Be(validOrder);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithoutColor_ReturnsFormattedString()
    {
        // Arrange
        var player = new SessionPlayer("Alice", 3);

        // Act
        var result = player.ToString();

        // Assert
        result.Should().Be("Alice (P3)");
    }

    [Fact]
    public void ToString_WithColor_IncludesColor()
    {
        // Arrange
        var player = new SessionPlayer("Bob", 2, "Green");

        // Act
        var result = player.ToString();

        // Assert
        result.Should().Be("Bob (Green, P2)");
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_PlayersWithSameNameAndOrder_AreEqual()
    {
        // Arrange - case-insensitive comparison for name
        var player1 = new SessionPlayer("John", 1);
        var player2 = new SessionPlayer("john", 1); // lowercase

        // Act & Assert
        player1.Should().Be(player2);
    }

    [Fact]
    public void Equals_PlayersWithDifferentNames_AreNotEqual()
    {
        // Arrange
        var player1 = new SessionPlayer("John", 1);
        var player2 = new SessionPlayer("Jane", 1);

        // Act & Assert
        player1.Should().NotBe(player2);
    }

    [Fact]
    public void Equals_PlayersWithDifferentOrders_AreNotEqual()
    {
        // Arrange
        var player1 = new SessionPlayer("John", 1);
        var player2 = new SessionPlayer("John", 2);

        // Act & Assert
        player1.Should().NotBe(player2);
    }

    [Fact]
    public void Equals_PlayersWithDifferentColors_AreEqual()
    {
        // Arrange - Color is not part of equality
        var player1 = new SessionPlayer("John", 1, "Red");
        var player2 = new SessionPlayer("John", 1, "Blue");

        // Act & Assert - Colors don't affect equality (only name and order matter)
        player1.Should().Be(player2);
    }

    [Fact]
    public void GetHashCode_SameNameAndOrder_ReturnsSameHashCode()
    {
        // Arrange
        var player1 = new SessionPlayer("Alice", 2);
        var player2 = new SessionPlayer("ALICE", 2); // different case

        // Act & Assert
        player1.GetHashCode().Should().Be(player2.GetHashCode());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_WithNullColor_SetsColorToNull()
    {
        // Arrange & Act
        var player = new SessionPlayer("Player", 1, null);

        // Assert
        player.Color.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithWhitespaceOnlyColor_SetsColor()
    {
        // Arrange & Act
        var player = new SessionPlayer("Player", 1, "   ");

        // Assert - Whitespace is trimmed but preserved as empty string
        player.Color.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSpecialCharactersInName_Succeeds()
    {
        // Arrange & Act
        var player = new SessionPlayer("Player-1_Test", 1);

        // Assert
        player.PlayerName.Should().Be("Player-1_Test");
    }

    [Fact]
    public void Constructor_WithUnicodeCharactersInName_Succeeds()
    {
        // Arrange & Act
        var player = new SessionPlayer("Spieler-Müller", 1);

        // Assert
        player.PlayerName.Should().Be("Spieler-Müller");
    }

    #endregion
}
