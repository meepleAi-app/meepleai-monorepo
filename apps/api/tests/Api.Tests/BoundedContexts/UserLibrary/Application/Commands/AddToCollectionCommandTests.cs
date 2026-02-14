using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Unit tests for AddToCollectionCommand and related domain logic.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class AddToCollectionCommandTests
{
    #region Command Creation Tests

    [Fact]
    public void AddToCollectionCommand_CreatesWithRequiredProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var entityType = EntityType.Player;

        // Act
        var command = new AddToCollectionCommand(userId, entityType, entityId);

        // Assert
        command.UserId.Should().Be(userId);
        command.EntityType.Should().Be(entityType);
        command.EntityId.Should().Be(entityId);
        command.Notes.Should().BeNull();
        command.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void AddToCollectionCommand_CreatesWithOptionalNotes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var notes = "This player is amazing at strategy games!";

        // Act
        var command = new AddToCollectionCommand(userId, EntityType.Player, entityId, false, notes);

        // Assert
        command.UserId.Should().Be(userId);
        command.EntityType.Should().Be(EntityType.Player);
        command.EntityId.Should().Be(entityId);
        command.Notes.Should().Be(notes);
        command.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void AddToCollectionCommand_CreatesWithFavoriteFlag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var command = new AddToCollectionCommand(userId, EntityType.Event, entityId, true);

        // Assert
        command.UserId.Should().Be(userId);
        command.EntityType.Should().Be(EntityType.Event);
        command.EntityId.Should().Be(entityId);
        command.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void AddToCollectionCommand_CreatesWithAllParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var notes = "My favorite AI agent";

        // Act
        var command = new AddToCollectionCommand(userId, EntityType.Agent, entityId, true, notes);

        // Assert
        command.UserId.Should().Be(userId);
        command.EntityType.Should().Be(EntityType.Agent);
        command.EntityId.Should().Be(entityId);
        command.Notes.Should().Be(notes);
        command.IsFavorite.Should().BeTrue();
    }

    [Theory]
    [InlineData(EntityType.Game)]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public void AddToCollectionCommand_SupportsAllEntityTypes(EntityType entityType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        var command = new AddToCollectionCommand(userId, entityType, entityId);

        // Assert
        command.EntityType.Should().Be(entityType);
    }

    #endregion

    #region UserCollectionEntry Creation Tests

    [Fact]
    public void UserCollectionEntry_ThrowsWhenUserIdEmpty()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Act
        var act = () => new UserCollectionEntry(entryId, Guid.Empty, EntityType.Player, entityId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserId*");
    }

    [Fact]
    public void UserCollectionEntry_ThrowsWhenEntityIdEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Act
        var act = () => new UserCollectionEntry(entryId, userId, EntityType.Player, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EntityId*");
    }

    [Fact]
    public void UserCollectionEntry_CreatesWithValidParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Act
        var entry = new UserCollectionEntry(entryId, userId, EntityType.Event, entityId);

        // Assert
        entry.Id.Should().Be(entryId);
        entry.UserId.Should().Be(userId);
        entry.EntityType.Should().Be(EntityType.Event);
        entry.EntityId.Should().Be(entityId);
        entry.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entry.IsFavorite.Should().BeFalse();
        entry.Notes.Should().BeNull();
    }

    [Fact]
    public void UserCollectionEntry_UpdateNotes_SetsNotes()
    {
        // Arrange
        var entry = new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), EntityType.Session, Guid.NewGuid());
        var notes = "These are my notes about the session";

        // Act
        entry.UpdateNotes(notes);

        // Assert
        entry.Notes.Should().Be(notes);
    }

    [Fact]
    public void UserCollectionEntry_UpdateNotes_WithNull_ClearsNotes()
    {
        // Arrange
        var entry = new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), EntityType.Document, Guid.NewGuid());
        entry.UpdateNotes("Initial notes");

        // Act
        entry.UpdateNotes(null);

        // Assert
        entry.Notes.Should().BeNull();
    }

    [Fact]
    public void UserCollectionEntry_MarkAsFavorite_SetsFavoriteTrue()
    {
        // Arrange
        var entry = new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), EntityType.ChatSession, Guid.NewGuid());

        // Act
        entry.MarkAsFavorite();

        // Assert
        entry.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void UserCollectionEntry_RemoveFavorite_SetsFavoriteFalse()
    {
        // Arrange
        var entry = new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), EntityType.Agent, Guid.NewGuid());
        entry.MarkAsFavorite();

        // Act
        entry.RemoveFavorite();

        // Assert
        entry.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void UserCollectionEntry_ToggleFavorite_TogglesStatus()
    {
        // Arrange
        var entry = new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), EntityType.Player, Guid.NewGuid());

        // Act & Assert - First toggle
        entry.ToggleFavorite();
        entry.IsFavorite.Should().BeTrue();

        // Act & Assert - Second toggle
        entry.ToggleFavorite();
        entry.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void UserCollectionEntry_SetFavorite_SetsExplicitValue()
    {
        // Arrange
        var entry = new UserCollectionEntry(Guid.NewGuid(), Guid.NewGuid(), EntityType.Event, Guid.NewGuid());

        // Act & Assert
        entry.SetFavorite(true);
        entry.IsFavorite.Should().BeTrue();

        entry.SetFavorite(false);
        entry.IsFavorite.Should().BeFalse();
    }

    #endregion
}
