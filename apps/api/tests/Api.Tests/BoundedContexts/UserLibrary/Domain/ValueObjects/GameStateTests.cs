using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Tests for the GameState value object and GameStateType enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 8
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameStateTests
{
    #region GameStateType Enum Tests

    [Fact]
    public void GameStateType_Nuovo_HasCorrectValue()
    {
        ((int)GameStateType.Nuovo).Should().Be(0);
    }

    [Fact]
    public void GameStateType_InPrestito_HasCorrectValue()
    {
        ((int)GameStateType.InPrestito).Should().Be(1);
    }

    [Fact]
    public void GameStateType_Wishlist_HasCorrectValue()
    {
        ((int)GameStateType.Wishlist).Should().Be(2);
    }

    [Fact]
    public void GameStateType_Owned_HasCorrectValue()
    {
        ((int)GameStateType.Owned).Should().Be(3);
    }

    [Fact]
    public void GameStateType_HasFourValues()
    {
        var values = Enum.GetValues<GameStateType>();
        values.Should().HaveCount(4);
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void Nuovo_ReturnsStateWithCorrectType()
    {
        // Act
        var state = GameState.Nuovo();

        // Assert
        state.Value.Should().Be(GameStateType.Nuovo);
        state.ChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        state.StateNotes.Should().BeNull();
    }

    [Fact]
    public void Nuovo_WithNotes_IncludesNotes()
    {
        // Arrange
        var notes = "Gift from a friend";

        // Act
        var state = GameState.Nuovo(notes);

        // Assert
        state.StateNotes.Should().Be(notes);
    }

    [Fact]
    public void InPrestito_ReturnsStateWithCorrectType()
    {
        // Act
        var state = GameState.InPrestito();

        // Assert
        state.Value.Should().Be(GameStateType.InPrestito);
    }

    [Fact]
    public void InPrestito_WithBorrowerInfo_IncludesInfo()
    {
        // Arrange
        var borrowerInfo = "Lent to Marco";

        // Act
        var state = GameState.InPrestito(borrowerInfo);

        // Assert
        state.StateNotes.Should().Be(borrowerInfo);
    }

    [Fact]
    public void Wishlist_ReturnsStateWithCorrectType()
    {
        // Act
        var state = GameState.Wishlist();

        // Assert
        state.Value.Should().Be(GameStateType.Wishlist);
    }

    [Fact]
    public void Owned_ReturnsStateWithCorrectType()
    {
        // Act
        var state = GameState.Owned();

        // Assert
        state.Value.Should().Be(GameStateType.Owned);
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public void CanTransitionTo_FromNuovo_AllowsAllTransitions()
    {
        // Arrange
        var state = GameState.Nuovo();

        // Assert
        state.CanTransitionTo(GameStateType.Owned).Should().BeTrue();
        state.CanTransitionTo(GameStateType.InPrestito).Should().BeTrue();
        state.CanTransitionTo(GameStateType.Wishlist).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromWishlist_DisallowsInPrestito()
    {
        // Arrange - Cannot loan a game you don't own yet
        var state = GameState.Wishlist();

        // Assert
        state.CanTransitionTo(GameStateType.InPrestito).Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromWishlist_AllowsOwned()
    {
        // Arrange - Can purchase a wishlisted game
        var state = GameState.Wishlist();

        // Assert
        state.CanTransitionTo(GameStateType.Owned).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromOwned_DisallowsNuovo()
    {
        // Arrange - Once owned, can't be "new" again
        var state = GameState.Owned();

        // Assert
        state.CanTransitionTo(GameStateType.Nuovo).Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromInPrestito_DisallowsNuovo()
    {
        // Arrange - If loaned out, it's not new
        var state = GameState.InPrestito();

        // Assert
        state.CanTransitionTo(GameStateType.Nuovo).Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromInPrestito_AllowsOwned()
    {
        // Arrange - Game returned from loan
        var state = GameState.InPrestito();

        // Assert
        state.CanTransitionTo(GameStateType.Owned).Should().BeTrue();
    }

    #endregion

    #region Availability Tests

    [Fact]
    public void IsAvailable_WhenNuovo_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Nuovo();

        // Assert
        state.IsAvailable().Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WhenOwned_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Owned();

        // Assert
        state.IsAvailable().Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WhenInPrestito_ReturnsFalse()
    {
        // Arrange
        var state = GameState.InPrestito();

        // Assert
        state.IsAvailable().Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_WhenWishlist_ReturnsFalse()
    {
        // Arrange
        var state = GameState.Wishlist();

        // Assert
        state.IsAvailable().Should().BeFalse();
    }

    #endregion

    #region Physical Ownership Tests

    [Fact]
    public void IsPhysicallyOwned_WhenNuovo_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Nuovo();

        // Assert
        state.IsPhysicallyOwned().Should().BeTrue();
    }

    [Fact]
    public void IsPhysicallyOwned_WhenOwned_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Owned();

        // Assert
        state.IsPhysicallyOwned().Should().BeTrue();
    }

    [Fact]
    public void IsPhysicallyOwned_WhenInPrestito_ReturnsTrue()
    {
        // Arrange - Still owned, just loaned out
        var state = GameState.InPrestito();

        // Assert
        state.IsPhysicallyOwned().Should().BeTrue();
    }

    [Fact]
    public void IsPhysicallyOwned_WhenWishlist_ReturnsFalse()
    {
        // Arrange
        var state = GameState.Wishlist();

        // Assert
        state.IsPhysicallyOwned().Should().BeFalse();
    }

    #endregion

    #region ToString and Implicit Conversion Tests

    [Fact]
    public void ToString_ReturnsStateTypeName()
    {
        // Arrange
        var state = GameState.Owned();

        // Act
        var result = state.ToString();

        // Assert
        result.Should().Be("Owned");
    }

    [Fact]
    public void ImplicitConversion_ToGameStateType_ReturnsValue()
    {
        // Arrange
        var state = GameState.InPrestito();

        // Act
        GameStateType value = state;

        // Assert
        value.Should().Be(GameStateType.InPrestito);
    }

    #endregion

    #region Notes Trimming Tests

    [Fact]
    public void FactoryMethods_TrimNotes()
    {
        // Arrange
        var notesWithWhitespace = "  Note with spaces  ";

        // Act
        var state = GameState.Nuovo(notesWithWhitespace);

        // Assert
        state.StateNotes.Should().Be("Note with spaces");
    }

    #endregion
}
