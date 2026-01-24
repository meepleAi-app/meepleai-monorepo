using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain.ValueObjects;

public sealed class GameStateTests
{
    #region Factory Methods Tests

    [Fact]
    public void Nuovo_CreatesNewGameState()
    {
        // Act
        var state = GameState.Nuovo("Appena comprato");

        // Assert
        state.Should().NotBeNull();
        state.Value.Should().Be(GameStateType.Nuovo);
        state.StateNotes.Should().Be("Appena comprato");
        state.ChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void InPrestito_CreatesOnLoanState()
    {
        // Act
        var state = GameState.InPrestito("Prestato a Mario");

        // Assert
        state.Value.Should().Be(GameStateType.InPrestito);
        state.StateNotes.Should().Be("Prestato a Mario");
    }

    [Fact]
    public void Wishlist_CreatesWishlistState()
    {
        // Act
        var state = GameState.Wishlist("Da comprare");

        // Assert
        state.Value.Should().Be(GameStateType.Wishlist);
        state.StateNotes.Should().Be("Da comprare");
    }

    [Fact]
    public void Owned_CreatesOwnedState()
    {
        // Act
        var state = GameState.Owned();

        // Assert
        state.Value.Should().Be(GameStateType.Owned);
        state.StateNotes.Should().BeNull();
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public void CanTransitionTo_FromWishlistToInPrestito_ReturnsFalse()
    {
        // Arrange
        var state = GameState.Wishlist();

        // Act
        var canTransition = state.CanTransitionTo(GameStateType.InPrestito);

        // Assert
        canTransition.Should().BeFalse("cannot loan a game on wishlist (not physically owned)");
    }

    [Fact]
    public void CanTransitionTo_FromWishlistToOwned_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Wishlist();

        // Act
        var canTransition = state.CanTransitionTo(GameStateType.Owned);

        // Assert
        canTransition.Should().BeTrue("can buy a wishlist game");
    }

    [Fact]
    public void CanTransitionTo_FromOwnedToNuovo_ReturnsFalse()
    {
        // Arrange
        var state = GameState.Owned();

        // Act
        var canTransition = state.CanTransitionTo(GameStateType.Nuovo);

        // Assert
        canTransition.Should().BeFalse("cannot mark an owned game as new");
    }

    [Fact]
    public void CanTransitionTo_FromOwnedToInPrestito_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Owned();

        // Act
        var canTransition = state.CanTransitionTo(GameStateType.InPrestito);

        // Assert
        canTransition.Should().BeTrue("can loan an owned game");
    }

    [Fact]
    public void CanTransitionTo_FromInPrestitoToOwned_ReturnsTrue()
    {
        // Arrange
        var state = GameState.InPrestito("Prestato");

        // Act
        var canTransition = state.CanTransitionTo(GameStateType.Owned);

        // Assert
        canTransition.Should().BeTrue("can return a loaned game to owned");
    }

    #endregion

    #region Helper Methods Tests

    [Fact]
    public void IsAvailable_NuovoState_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Nuovo();

        // Act
        var isAvailable = state.IsAvailable();

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_OwnedState_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Owned();

        // Act
        var isAvailable = state.IsAvailable();

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_InPrestitoState_ReturnsFalse()
    {
        // Arrange
        var state = GameState.InPrestito();

        // Act
        var isAvailable = state.IsAvailable();

        // Assert
        isAvailable.Should().BeFalse("game is on loan");
    }

    [Fact]
    public void IsAvailable_WishlistState_ReturnsFalse()
    {
        // Arrange
        var state = GameState.Wishlist();

        // Act
        var isAvailable = state.IsAvailable();

        // Assert
        isAvailable.Should().BeFalse("game is not owned yet");
    }

    [Fact]
    public void IsPhysicallyOwned_WishlistState_ReturnsFalse()
    {
        // Arrange
        var state = GameState.Wishlist();

        // Act
        var isOwned = state.IsPhysicallyOwned();

        // Assert
        isOwned.Should().BeFalse();
    }

    [Fact]
    public void IsPhysicallyOwned_OwnedState_ReturnsTrue()
    {
        // Arrange
        var state = GameState.Owned();

        // Act
        var isOwned = state.IsPhysicallyOwned();

        // Assert
        isOwned.Should().BeTrue();
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_TwoStatesWithSameValueButDifferentTimestamps_AreNotEqual()
    {
        // Arrange
        var state1 = GameState.Owned("Note");
        Thread.Sleep(10); // Ensure different timestamps
        var state2 = GameState.Owned("Note");

        // Act & Assert
        state1.Should().NotBe(state2, "ChangedAt timestamp is part of equality");
    }

    [Fact]
    public void Equals_TwoStatesWithDifferentValue_AreNotEqual()
    {
        // Arrange
        var state1 = GameState.Nuovo();
        var state2 = GameState.Owned();

        // Act & Assert
        state1.Should().NotBe(state2);
    }

    [Fact]
    public void ImplicitConversion_ToGameStateType_Works()
    {
        // Arrange
        var state = GameState.Owned();

        // Act
        GameStateType stateType = state;

        // Assert
        stateType.Should().Be(GameStateType.Owned);
    }

    #endregion
}
