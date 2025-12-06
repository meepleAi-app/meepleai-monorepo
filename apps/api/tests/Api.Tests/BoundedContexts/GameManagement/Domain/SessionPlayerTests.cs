using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]

public class SessionPlayerTests
{
    [Fact]
    public void SessionPlayer_WithValidData_CreatesSuccessfully()
    {
        // Arrange & Act
        var player = new SessionPlayer("Alice", 1, "Red");

        // Assert
        Assert.Equal("Alice", player.PlayerName);
        Assert.Equal(1, player.PlayerOrder);
        Assert.Equal("Red", player.Color);
    }

    [Fact]
    public void SessionPlayer_WithoutColor_CreatesSuccessfully()
    {
        // Arrange & Act
        var player = new SessionPlayer("Bob", 2);

        // Assert
        Assert.Equal("Bob", player.PlayerName);
        Assert.Equal(2, player.PlayerOrder);
        Assert.Null(player.Color);
    }

    [Fact]
    public void SessionPlayer_TrimsWhitespace()
    {
        // Arrange & Act
        var player = new SessionPlayer("  Alice  ", 1, "  Red  ");

        // Assert
        Assert.Equal("Alice", player.PlayerName);
        Assert.Equal("Red", player.Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SessionPlayer_WithEmptyName_ThrowsValidationException(string invalidName)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new SessionPlayer(invalidName, 1));
        Assert.Contains("Player name cannot be empty", exception.Message);
    }

    [Fact]
    public void SessionPlayer_ExceedingMaxLength_ThrowsValidationException()
    {
        // Arrange
        var longName = new string('a', 51);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new SessionPlayer(longName, 1));
        Assert.Contains("cannot exceed 50 characters", exception.Message);
    }

    [Fact]
    public void SessionPlayer_OrderBelowOne_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new SessionPlayer("Alice", 0));
        Assert.Contains("must be at least 1", exception.Message);
    }

    [Fact]
    public void SessionPlayer_OrderAbove100_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new SessionPlayer("Alice", 101));
        Assert.Contains("cannot exceed 100", exception.Message);
    }

    [Fact]
    public void SessionPlayer_ToString_FormatsCorrectly()
    {
        // Arrange
        var withColor = new SessionPlayer("Alice", 1, "Red");
        var withoutColor = new SessionPlayer("Bob", 2);

        // Act & Assert
        Assert.Equal("Alice (Red, P1)", withColor.ToString());
        Assert.Equal("Bob (P2)", withoutColor.ToString());
    }

    [Fact]
    public void SessionPlayer_EqualityComparison_IsCaseInsensitive()
    {
        // Arrange
        var player1 = new SessionPlayer("Alice", 1);
        var player2 = new SessionPlayer("ALICE", 1);
        var player3 = new SessionPlayer("Bob", 1);

        // Act & Assert
        Assert.Equal(player1, player2); // Same name (case-insensitive) + same order
        Assert.NotEqual(player1, player3);
    }
}

