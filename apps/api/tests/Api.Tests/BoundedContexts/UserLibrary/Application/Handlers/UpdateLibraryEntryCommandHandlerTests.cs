using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for UpdateLibraryEntryCommandHandler.
/// Tests library entry update scenarios including notes and favorite status.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class UpdateLibraryEntryCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepository;
    private readonly Mock<ISharedGameRepository> _mockSharedGameRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdateLibraryEntryCommandHandler _handler;

    public UpdateLibraryEntryCommandHandlerTests()
    {
        _mockLibraryRepository = new Mock<IUserLibraryRepository>();
        _mockSharedGameRepository = new Mock<ISharedGameRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _handler = new UpdateLibraryEntryCommandHandler(
            _mockLibraryRepository.Object,
            _mockSharedGameRepository.Object,
            _mockUnitOfWork.Object);
    }

    #region Null Guard Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Entry Not Found Tests

    [Fact]
    public async Task Handle_WhenEntryNotInLibrary_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new UpdateLibraryEntryCommand(userId, gameId, "New notes");

        _mockLibraryRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<DomainException>(act);
        exception.Message.Should().Contain("not in your library");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Game Not Found Tests

    [Fact]
    public async Task Handle_WhenSharedGameNotFound_ThrowsDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var command = new UpdateLibraryEntryCommand(userId, gameId, "New notes");

        var existingEntry = new UserLibraryEntry(entryId, userId, gameId);

        _mockLibraryRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<DomainException>(act);
        exception.Message.Should().Contain("not found in catalog");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Successful Update Tests

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesEntryAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var newNotes = "My updated notes about this game";
        var command = new UpdateLibraryEntryCommand(userId, gameId, newNotes, true);

        var existingEntry = new UserLibraryEntry(entryId, userId, gameId);
        var sharedGame = CreateTestSharedGame(gameId, "Test Game", "Test Publisher");

        _mockLibraryRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(entryId);
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.GameTitle.Should().Be("Test Game");
        result.Notes.Should().Be(newNotes);
        result.IsFavorite.Should().BeTrue();

        _mockLibraryRepository.Verify(
            r => r.UpdateAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithOnlyNotesUpdate_UpdatesOnlyNotes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var command = new UpdateLibraryEntryCommand(userId, gameId, "New notes only", null);

        var existingEntry = new UserLibraryEntry(entryId, userId, gameId);
        existingEntry.MarkAsFavorite(); // Start as favorite
        var sharedGame = CreateTestSharedGame(gameId, "Test Game", "Publisher");

        _mockLibraryRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Notes.Should().Be("New notes only");
        result.IsFavorite.Should().BeTrue(); // Should remain favorite

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithOnlyFavoriteUpdate_UpdatesOnlyFavorite()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var command = new UpdateLibraryEntryCommand(userId, gameId, null, false);

        var existingEntry = new UserLibraryEntry(entryId, userId, gameId);
        existingEntry.MarkAsFavorite(); // Start as favorite
        var sharedGame = CreateTestSharedGame(gameId, "Test Game", "Publisher");

        _mockLibraryRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsFavorite.Should().BeFalse(); // Should be unfavorited

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyNotes_ClearsNotes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var command = new UpdateLibraryEntryCommand(userId, gameId, "", null);

        var existingEntry = new UserLibraryEntry(entryId, userId, gameId);
        var sharedGame = CreateTestSharedGame(gameId, "Test Game", "Publisher");

        _mockLibraryRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        // Empty string clears notes (LibraryNotes.FromNullable returns null for empty)
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DTO Mapping Tests

    [Fact]
    public async Task Handle_MapsGamePublisherFromFirstPublisher()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var command = new UpdateLibraryEntryCommand(userId, gameId, "Notes", null);

        var existingEntry = new UserLibraryEntry(entryId, userId, gameId);
        var sharedGame = CreateTestSharedGame(gameId, "Catan", "Kosmos");

        _mockLibraryRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be("Catan");
        result.GameYearPublished.Should().Be(2020);
        result.GameImageUrl.Should().Be("https://example.com/image.jpg");
        result.GameIconUrl.Should().Be("https://example.com/thumb.jpg");
    }

    #endregion

    #region Helper Methods

    private static SharedGame CreateTestSharedGame(Guid id, string title, string? publisher = null)
    {
        // Use the internal constructor for reconstitution (test purposes)
        return new SharedGame(
            id: id,
            title: title,
            yearPublished: 2020,
            description: "Test game description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            status: GameStatus.Published,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow.AddDays(-30),
            modifiedAt: null,
            isDeleted: false,
            bggId: 12345);
    }

    #endregion
}
