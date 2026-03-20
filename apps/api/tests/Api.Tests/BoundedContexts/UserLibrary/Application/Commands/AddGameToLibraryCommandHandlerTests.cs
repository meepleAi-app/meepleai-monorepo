using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Unit tests for AddGameToLibraryCommand and related domain logic.
/// The AddGameToLibraryCommandHandler requires complex DbContext setup for quota checks,
/// so full handler testing is covered by endpoint integration tests.
/// These tests focus on command creation and domain entity behavior.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class AddGameToLibraryCommandHandlerTests
{
    #region Command Creation Tests

    [Fact]
    public void AddGameToLibraryCommand_CreatesWithRequiredProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var command = new AddGameToLibraryCommand(userId, gameId);

        // Assert
        command.UserId.Should().Be(userId);
        command.GameId.Should().Be(gameId);
        command.Notes.Should().BeNull();
        command.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void AddGameToLibraryCommand_CreatesWithOptionalNotes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var notes = "This is a great game for family game night!";

        // Act
        var command = new AddGameToLibraryCommand(userId, gameId, notes);

        // Assert
        command.UserId.Should().Be(userId);
        command.GameId.Should().Be(gameId);
        command.Notes.Should().Be(notes);
        command.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void AddGameToLibraryCommand_CreatesWithFavoriteFlag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var command = new AddGameToLibraryCommand(userId, gameId, null, true);

        // Assert
        command.UserId.Should().Be(userId);
        command.GameId.Should().Be(gameId);
        command.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void AddGameToLibraryCommand_CreatesWithAllParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var notes = "My favorite strategy game";

        // Act
        var command = new AddGameToLibraryCommand(userId, gameId, notes, true);

        // Assert
        command.UserId.Should().Be(userId);
        command.GameId.Should().Be(gameId);
        command.Notes.Should().Be(notes);
        command.IsFavorite.Should().BeTrue();
    }

    #endregion

    #region UserLibraryEntry Creation Tests

    [Fact]
    public void UserLibraryEntry_ThrowsWhenUserIdEmpty()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Act
        var act = () => new UserLibraryEntry(entryId, Guid.Empty, gameId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserId*");
    }

    [Fact]
    public void UserLibraryEntry_ThrowsWhenGameIdEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Act
        var act = () => new UserLibraryEntry(entryId, userId, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*GameId*");
    }

    [Fact]
    public void UserLibraryEntry_CreatesWithValidParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Act
        var entry = new UserLibraryEntry(entryId, userId, gameId);

        // Assert
        entry.Id.Should().Be(entryId);
        entry.UserId.Should().Be(userId);
        entry.GameId.Should().Be(gameId);
        entry.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entry.IsFavorite.Should().BeFalse();
        entry.Notes.Should().BeNull();
    }

    [Fact]
    public void UserLibraryEntry_UpdateNotes_SetsNotes()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var notes = LibraryNotes.FromNullable("These are my notes about the game");

        // Act
        entry.UpdateNotes(notes);

        // Assert
        entry.Notes.Should().Be(notes);
        entry.Notes?.Value.Should().Be("These are my notes about the game");
    }

    [Fact]
    public void UserLibraryEntry_UpdateNotes_WithNull_ClearsNotes()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.UpdateNotes(LibraryNotes.FromNullable("Initial notes"));

        // Act
        entry.UpdateNotes(null);

        // Assert
        entry.Notes.Should().BeNull();
    }

    [Fact]
    public void UserLibraryEntry_MarkAsFavorite_SetsFavoriteTrue()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        entry.MarkAsFavorite();

        // Assert
        entry.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void UserLibraryEntry_RemoveFavorite_SetsFavoriteFalse()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.MarkAsFavorite();

        // Act
        entry.RemoveFavorite();

        // Assert
        entry.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void UserLibraryEntry_ToggleFavorite_TogglesStatus()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert - First toggle
        entry.ToggleFavorite();
        entry.IsFavorite.Should().BeTrue();

        // Act & Assert - Second toggle
        entry.ToggleFavorite();
        entry.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void UserLibraryEntry_SetFavorite_SetsExplicitValue()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        entry.SetFavorite(true);
        entry.IsFavorite.Should().BeTrue();

        entry.SetFavorite(false);
        entry.IsFavorite.Should().BeFalse();
    }

    #endregion

    #region LibraryNotes Value Object Tests

    [Fact]
    public void LibraryNotes_FromNullable_WithValidString_ReturnsNotes()
    {
        // Arrange
        var notesText = "This is a valid note";

        // Act
        var notes = LibraryNotes.FromNullable(notesText);

        // Assert
        notes.Should().NotBeNull();
        notes!.Value.Should().Be(notesText);
    }

    [Fact]
    public void LibraryNotes_FromNullable_WithNull_ReturnsNull()
    {
        // Act
        var notes = LibraryNotes.FromNullable(null);

        // Assert
        notes.Should().BeNull();
    }

    [Fact]
    public void LibraryNotes_FromNullable_WithEmptyString_ReturnsNull()
    {
        // Act
        var notes = LibraryNotes.FromNullable("");

        // Assert
        notes.Should().BeNull();
    }

    [Fact]
    public void LibraryNotes_FromNullable_WithWhitespace_ReturnsNull()
    {
        // Act
        var notes = LibraryNotes.FromNullable("   ");

        // Assert
        notes.Should().BeNull();
    }

    [Fact]
    public void LibraryNotes_FromNullable_TrimsWhitespace()
    {
        // Arrange
        var notesText = "  My note with whitespace  ";

        // Act
        var notes = LibraryNotes.FromNullable(notesText);

        // Assert
        notes.Should().NotBeNull();
        notes!.Value.Should().Be("My note with whitespace");
    }

    #endregion
}
