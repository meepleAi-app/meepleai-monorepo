using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the DeclareOwnership method on UserLibraryEntry.
/// Ownership declaration grants RAG access to the game's knowledge base.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UserLibraryEntryDeclareOwnershipTests
{
    [Fact]
    public void DeclareOwnership_WhenNuovo_TransitionsToOwnedAndSetsOwnershipDeclaredAt()
    {
        // Arrange
        var entry = CreateEntry(); // default state is Nuovo
        entry.ClearDomainEvents();

        // Act
        entry.DeclareOwnership();

        // Assert
        entry.HasDeclaredOwnership.Should().BeTrue();
        entry.OwnershipDeclaredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entry.CurrentState.Value.Should().Be(GameStateType.Owned);
        entry.DomainEvents.Should().HaveCountGreaterThanOrEqualTo(1);
        entry.DomainEvents.OfType<OwnershipDeclaredEvent>().Should().ContainSingle();
    }

    [Fact]
    public void DeclareOwnership_WhenWishlist_ThrowsDomainException()
    {
        // Arrange
        var entry = CreateEntry();
        entry.AddToWishlist();
        entry.ClearDomainEvents();

        // Act
        var action = () => entry.DeclareOwnership();

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*wishlist*");
        entry.HasDeclaredOwnership.Should().BeFalse();
        entry.OwnershipDeclaredAt.Should().BeNull();
    }

    [Fact]
    public void DeclareOwnership_WhenInPrestito_StaysInPrestitoAndSetsOwnershipDeclaredAt()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsOwned();
        entry.MarkAsOnLoan("Alice");
        entry.ClearDomainEvents();

        // Act
        entry.DeclareOwnership();

        // Assert
        entry.HasDeclaredOwnership.Should().BeTrue();
        entry.OwnershipDeclaredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entry.CurrentState.Value.Should().Be(GameStateType.InPrestito);
        entry.DomainEvents.OfType<OwnershipDeclaredEvent>().Should().ContainSingle();
    }

    [Fact]
    public void DeclareOwnership_WhenAlreadyOwnedButNotDeclared_SetsOwnershipDeclaredAt()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsOwned();
        entry.ClearDomainEvents();

        // Act
        entry.DeclareOwnership();

        // Assert
        entry.HasDeclaredOwnership.Should().BeTrue();
        entry.OwnershipDeclaredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entry.CurrentState.Value.Should().Be(GameStateType.Owned);
        entry.DomainEvents.OfType<OwnershipDeclaredEvent>().Should().ContainSingle();
    }

    [Fact]
    public void DeclareOwnership_WhenAlreadyDeclared_IsIdempotentNoOp()
    {
        // Arrange
        var entry = CreateEntry();
        entry.DeclareOwnership();
        var firstDeclaredAt = entry.OwnershipDeclaredAt;
        entry.ClearDomainEvents();

        // Act
        entry.DeclareOwnership();

        // Assert
        entry.HasDeclaredOwnership.Should().BeTrue();
        entry.OwnershipDeclaredAt.Should().Be(firstDeclaredAt);
        entry.DomainEvents.Should().BeEmpty("idempotent call should not emit events");
    }

    #region Helpers

    private static UserLibraryEntry CreateEntry()
    {
        return new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    #endregion
}
