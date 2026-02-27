using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetGamePdfIndexingStatusQueryHandler.
/// Issue #4943: PDF indexing status polling endpoint.
/// Issue #5217: Extended to support shared catalog game IDs.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetGamePdfIndexingStatusQueryHandlerTests
{
    private readonly Mock<IVectorDocumentRepository> _vectorRepoMock;
    private readonly Mock<IPrivateGameRepository> _privateGameRepoMock;
    private readonly Mock<IUserLibraryRepository> _userLibraryRepoMock;
    private readonly Mock<ILogger<GetGamePdfIndexingStatusQueryHandler>> _loggerMock;
    private readonly GetGamePdfIndexingStatusQueryHandler _handler;

    public GetGamePdfIndexingStatusQueryHandlerTests()
    {
        _vectorRepoMock = new Mock<IVectorDocumentRepository>();
        _privateGameRepoMock = new Mock<IPrivateGameRepository>();
        _userLibraryRepoMock = new Mock<IUserLibraryRepository>();
        _loggerMock = new Mock<ILogger<GetGamePdfIndexingStatusQueryHandler>>();

        _handler = new GetGamePdfIndexingStatusQueryHandler(
            _vectorRepoMock.Object,
            _privateGameRepoMock.Object,
            _userLibraryRepoMock.Object,
            _loggerMock.Object);
    }

    // ──────────────────────────────────────────────────
    // Authorization / Not-Found gates
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Not a private game AND not in user's shared library → 404
        _userLibraryRepoMock
            .Setup(r => r.IsGameInLibraryAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));
    }

    // ──────────────────────────────────────────────────
    // Issue #5217: Shared catalog game path (Path B)
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SharedCatalogGame_InLibrary_ReturnsStatus()
    {
        // Arrange — game is NOT a private game but IS in user's shared library
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var info = new VectorDocumentIndexingInfo(VectorDocumentIndexingStatus.Completed, 15, null);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        _userLibraryRepoMock
            .Setup(r => r.IsGameInLibraryAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _vectorRepoMock
            .Setup(r => r.GetIndexingInfoByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(info);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("indexed", result.Status);
        Assert.Equal(100, result.Progress);
        Assert.Equal(15, result.ChunkCount);
    }

    [Fact]
    public async Task Handle_SharedCatalogGame_NotInLibrary_ThrowsNotFoundException()
    {
        // Arrange — game is NOT a private game and NOT in user's shared library
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        _userLibraryRepoMock
            .Setup(r => r.IsGameInLibraryAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_UserDoesNotOwnGame_ThrowsForbiddenException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();

        var game = PrivateGame.CreateManual(
            ownerId: ownerId,
            title: "Test Game",
            minPlayers: 2,
            maxPlayers: 4,
            yearPublished: 2024,
            description: "A test game",
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.0m,
            imageUrl: null);

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var query = new GetGamePdfIndexingStatusQuery(gameId, differentUserId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_NoVectorDocumentForGame_ThrowsNotFoundException()
    {
        // Arrange
        var (gameId, userId, game) = CreateGameWithOwner();

        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _vectorRepoMock
            .Setup(r => r.GetIndexingInfoByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorDocumentIndexingInfo?)null);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));
    }

    // ──────────────────────────────────────────────────
    // Status mapping
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PendingStatus_ReturnsPendingWithZeroProgress()
    {
        // Arrange
        var (gameId, userId, game) = CreateGameWithOwner();
        var info = new VectorDocumentIndexingInfo(VectorDocumentIndexingStatus.Pending, 0, null);

        ArrangeSuccess(gameId, game, info);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("pending", result.Status);
        Assert.Equal(0, result.Progress);
        Assert.Null(result.ChunkCount);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ProcessingStatus_ReturnsProcessingWithNullProgress()
    {
        // Arrange
        var (gameId, userId, game) = CreateGameWithOwner();
        var info = new VectorDocumentIndexingInfo(VectorDocumentIndexingStatus.Processing, 0, null);

        ArrangeSuccess(gameId, game, info);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("processing", result.Status);
        Assert.Null(result.Progress);
        Assert.Null(result.ChunkCount);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_CompletedStatus_ReturnsIndexedWith100ProgressAndChunkCount()
    {
        // Arrange
        var (gameId, userId, game) = CreateGameWithOwner();
        var info = new VectorDocumentIndexingInfo(VectorDocumentIndexingStatus.Completed, 42, null);

        ArrangeSuccess(gameId, game, info);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert: "completed" is mapped to "indexed" for the public API
        Assert.Equal("indexed", result.Status);
        Assert.Equal(100, result.Progress);
        Assert.Equal(42, result.ChunkCount);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_FailedStatus_ReturnsFailedWithErrorMessage()
    {
        // Arrange
        var (gameId, userId, game) = CreateGameWithOwner();
        var info = new VectorDocumentIndexingInfo(VectorDocumentIndexingStatus.Failed, 0, "Embedding service unavailable");

        ArrangeSuccess(gameId, game, info);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("failed", result.Status);
        Assert.Null(result.Progress);
        Assert.Null(result.ChunkCount);
        Assert.Equal("Embedding service unavailable", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_CompletedStatus_DoesNotExposeErrorMessage()
    {
        // Arrange
        var (gameId, userId, game) = CreateGameWithOwner();
        // Even if an error is accidentally stored, completed should not surface it
        var info = new VectorDocumentIndexingInfo(VectorDocumentIndexingStatus.Completed, 10, "stale error");

        ArrangeSuccess(gameId, game, info);

        var query = new GetGamePdfIndexingStatusQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("indexed", result.Status);
        Assert.Null(result.ErrorMessage);
    }

    // ──────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────

    private static (Guid gameId, Guid userId, PrivateGame game) CreateGameWithOwner()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var game = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Test Game",
            minPlayers: 2,
            maxPlayers: 4,
            yearPublished: 2024,
            description: "A test game",
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.0m,
            imageUrl: null);
        return (gameId, userId, game);
    }

    private void ArrangeSuccess(Guid gameId, PrivateGame game, VectorDocumentIndexingInfo info)
    {
        _privateGameRepoMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _vectorRepoMock
            .Setup(r => r.GetIndexingInfoByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(info);
    }
}
