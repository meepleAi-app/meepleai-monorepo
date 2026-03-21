using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Unit tests for GamePreAddedHandler.
/// Verifies pre-adding games to user library with idempotency checks.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GamePreAddedHandlerTests
{
    private readonly Mock<ISharedGameRepository> _mockSharedGameRepo = new();
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo = new();
    private readonly Mock<IGameSuggestionRepository> _mockGameSuggestionRepo = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly Mock<ILogger<GamePreAddedHandler>> _mockLogger = new();
    private readonly GamePreAddedHandler _handler;

    public GamePreAddedHandlerTests()
    {
        _handler = new GamePreAddedHandler(
            _mockSharedGameRepo.Object,
            _mockLibraryRepo.Object,
            _mockGameSuggestionRepo.Object,
            _timeProvider,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullSharedGameRepo_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GamePreAddedHandler(
            null!,
            _mockLibraryRepo.Object,
            _mockGameSuggestionRepo.Object,
            _timeProvider,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sharedGameRepo");
    }

    [Fact]
    public void Constructor_WithNullLibraryRepo_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GamePreAddedHandler(
            _mockSharedGameRepo.Object,
            null!,
            _mockGameSuggestionRepo.Object,
            _timeProvider,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("libraryRepo");
    }

    [Fact]
    public void Constructor_WithNullGameSuggestionRepo_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GamePreAddedHandler(
            _mockSharedGameRepo.Object,
            _mockLibraryRepo.Object,
            null!,
            _timeProvider,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gameSuggestionRepo");
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GamePreAddedHandler(
            _mockSharedGameRepo.Object,
            _mockLibraryRepo.Object,
            _mockGameSuggestionRepo.Object,
            null!,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("timeProvider");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GamePreAddedHandler(
            _mockSharedGameRepo.Object,
            _mockLibraryRepo.Object,
            _mockGameSuggestionRepo.Object,
            _timeProvider,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenGameNotInCatalog_SkipsProcessing()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        _mockSharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act
        await _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        _mockLibraryRepo.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenGameAlreadyInLibrary_SkipsAddingToLibrary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        SharedGame sharedGame = SharedGame.CreateSkeleton("Test Game", Guid.NewGuid(), _timeProvider);

        _mockSharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        _mockLibraryRepo
            .Setup(r => r.IsGameInLibraryAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        _mockLibraryRepo.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenGameExistsAndNotInLibrary_AddsToLibrary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        SharedGame sharedGame = SharedGame.CreateSkeleton("Test Game", Guid.NewGuid(), _timeProvider);

        _mockSharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        _mockLibraryRepo
            .Setup(r => r.IsGameInLibraryAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockGameSuggestionRepo
            .Setup(r => r.ExistsForUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        _mockLibraryRepo.Verify(
            r => r.AddAsync(
                It.Is<UserLibraryEntry>(e => e.UserId == userId && e.GameId == gameId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenGameExistsAndNotInLibrary_AlsoCreatesSuggestion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        SharedGame sharedGame = SharedGame.CreateSkeleton("Test Game", Guid.NewGuid(), _timeProvider);

        _mockSharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        _mockLibraryRepo
            .Setup(r => r.IsGameInLibraryAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockGameSuggestionRepo
            .Setup(r => r.ExistsForUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        _mockGameSuggestionRepo.Verify(
            r => r.AddAsync(
                It.Is<GameSuggestion>(s => s.UserId == userId && s.GameId == gameId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSuggestionAlreadyExists_DoesNotCreateDuplicateSuggestion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestedByUserId = Guid.NewGuid();

        SharedGame sharedGame = SharedGame.CreateSkeleton("Test Game", Guid.NewGuid(), _timeProvider);

        _mockSharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        _mockLibraryRepo
            .Setup(r => r.IsGameInLibraryAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockGameSuggestionRepo
            .Setup(r => r.ExistsForUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(userId, gameId, suggestedByUserId, TestContext.Current.CancellationToken);

        // Assert
        _mockGameSuggestionRepo.Verify(
            r => r.AddAsync(It.IsAny<GameSuggestion>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
