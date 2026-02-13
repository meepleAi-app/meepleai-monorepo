using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the UserCollectionEntry aggregate root.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public sealed class UserCollectionEntryTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesEntry()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var entityType = EntityType.Player;

        // Act
        var entry = new UserCollectionEntry(id, userId, entityType, entityId);

        // Assert
        entry.Id.Should().Be(id);
        entry.UserId.Should().Be(userId);
        entry.EntityType.Should().Be(entityType);
        entry.EntityId.Should().Be(entityId);
        entry.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entry.IsFavorite.Should().BeFalse();
        entry.Notes.Should().BeNull();
        entry.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_RaisesDomainEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var entry = new UserCollectionEntry(id, userId, EntityType.Event, entityId);

        // Assert
        entry.DomainEvents.Should().ContainSingle();
        entry.DomainEvents.First().Should().BeOfType<ItemAddedToCollectionEvent>();
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var action = () => new UserCollectionEntry(Guid.NewGuid(), Guid.Empty, EntityType.Player, Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyEntityId_ThrowsArgumentException()
    {
        // Act
        var action = () => new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), EntityType.Player, Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("entityId")
            .WithMessage("*EntityId cannot be empty*");
    }

    [Theory]
    [InlineData((EntityType)999)]
    [InlineData((EntityType)(-1))]
    public void Constructor_WithInvalidEntityType_ThrowsArgumentException(EntityType invalidType)
    {
        // Act
        var action = () => new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), invalidType, Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("entityType")
            .WithMessage("*Invalid EntityType*");
    }

    [Theory]
    [InlineData(EntityType.Game)]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public void Constructor_WithAllValidEntityTypes_CreatesEntry(EntityType entityType)
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var entry = new UserCollectionEntry(id, userId, entityType, entityId);

        // Assert
        entry.EntityType.Should().Be(entityType);
        entry.DomainEvents.Should().ContainSingle();
    }

    #endregion

    #region Favorite Tests

    [Fact]
    public void ToggleFavorite_WhenNotFavorite_SetsFavoriteTrue()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.ToggleFavorite();

        // Assert
        entry.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void ToggleFavorite_WhenFavorite_SetsFavoriteFalse()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsFavorite();

        // Act
        entry.ToggleFavorite();

        // Assert
        entry.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void MarkAsFavorite_SetsFavoriteTrue()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.MarkAsFavorite();

        // Assert
        entry.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void RemoveFavorite_SetsFavoriteFalse()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsFavorite();

        // Act
        entry.RemoveFavorite();

        // Assert
        entry.IsFavorite.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetFavorite_SetsExplicitValue(bool isFavorite)
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.SetFavorite(isFavorite);

        // Assert
        entry.IsFavorite.Should().Be(isFavorite);
    }

    #endregion

    #region Notes Tests

    [Fact]
    public void UpdateNotes_WithValidString_SetsNotes()
    {
        // Arrange
        var entry = CreateEntry();
        var notes = "Great player to follow";

        // Act
        entry.UpdateNotes(notes);

        // Assert
        entry.Notes.Should().Be(notes);
    }

    [Fact]
    public void UpdateNotes_WithNull_ClearsNotes()
    {
        // Arrange
        var entry = CreateEntry();
        entry.UpdateNotes("Some notes");

        // Act
        entry.UpdateNotes(null);

        // Assert
        entry.Notes.Should().BeNull();
    }

    [Fact]
    public void UpdateNotes_WithEmptyString_ClearsNotes()
    {
        // Arrange
        var entry = CreateEntry();
        entry.UpdateNotes("Some notes");

        // Act
        entry.UpdateNotes("");

        // Assert
        entry.Notes.Should().BeNullOrEmpty();
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void UpdateMetadata_WithValidMetadata_SetsMetadata()
    {
        // Arrange
        var entry = CreateEntry();
        var metadata = CollectionMetadata.Create(new Dictionary<string, object?>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        });

        // Act
        entry.UpdateMetadata(metadata);

        // Assert
        entry.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void UpdateMetadata_WithNull_SetsEmptyMetadata()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.UpdateMetadata(null!);

        // Assert
        entry.Metadata.Should().NotBeNull();
        entry.Metadata.Should().Be(CollectionMetadata.Empty());
    }

    #endregion

    #region PrepareForRemoval Tests

    [Fact]
    public void PrepareForRemoval_RaisesItemRemovedFromCollectionEvent()
    {
        // Arrange
        var entry = CreateEntry();
        entry.ClearDomainEvents();

        // Act
        entry.PrepareForRemoval();

        // Assert
        entry.DomainEvents.Should().ContainSingle();
        entry.DomainEvents.First().Should().BeOfType<ItemRemovedFromCollectionEvent>();
    }

    [Fact]
    public void PrepareForRemoval_RaisesEventWithCorrectProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var entityType = EntityType.Event;
        var entry = new UserCollectionEntry(id, userId, entityType, entityId);
        entry.ClearDomainEvents();

        // Act
        entry.PrepareForRemoval();

        // Assert
        var evt = entry.DomainEvents.First().Should().BeOfType<ItemRemovedFromCollectionEvent>().Subject;
        evt.EntryId.Should().Be(id);
        evt.UserId.Should().Be(userId);
        evt.EntityType.Should().Be(entityType);
        evt.EntityId.Should().Be(entityId);
    }

    #endregion

    #region Helper Methods

    private static UserCollectionEntry CreateEntry(EntityType entityType = EntityType.Player)
    {
        return new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), entityType, Guid.NewGuid());
    }

    #endregion
}
