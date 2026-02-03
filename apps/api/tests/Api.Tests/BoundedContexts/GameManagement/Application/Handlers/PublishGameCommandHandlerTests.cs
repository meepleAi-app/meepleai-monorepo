using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Issue #3481: Unit tests for PublishGameCommandHandler.
/// Tests publication workflow with approval status transitions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class PublishGameCommandHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly PublishGameCommandHandler _handler;

    public PublishGameCommandHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new PublishGameCommandHandler(
            _gameRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithApprovedStatus_PublishesGameSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Test Game")
            .WithSharedGameLink(sharedGameId)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new PublishGameCommand(
            GameId: gameId,
            Status: ApprovalStatus.Approved);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.Id);
        Assert.True(result.IsPublished);
        Assert.Equal(ApprovalStatus.Approved.ToString(), result.ApprovalStatus);
        Assert.NotNull(result.PublishedAt);

        // Verify repository interactions
        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
        _gameRepositoryMock.Verify(
            r => r.UpdateAsync(game, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPendingReviewStatus_SetsStatusWithoutPublishing()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Test Game")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new PublishGameCommand(
            GameId: gameId,
            Status: ApprovalStatus.PendingReview);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsPublished);
        Assert.Equal(ApprovalStatus.PendingReview.ToString(), result.ApprovalStatus);
        Assert.Null(result.PublishedAt);
    }

    [Fact]
    public async Task Handle_WithRejectedStatus_SetsStatusWithoutPublishing()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Test Game")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new PublishGameCommand(
            GameId: gameId,
            Status: ApprovalStatus.Rejected);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsPublished);
        Assert.Equal(ApprovalStatus.Rejected.ToString(), result.ApprovalStatus);
        Assert.Null(result.PublishedAt);
    }

    [Fact]
    public async Task Handle_NonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var command = new PublishGameCommand(
            GameId: gameId,
            Status: ApprovalStatus.Approved);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains($"Game with ID {gameId} not found", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Verify update was NOT called
        _gameRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ApproveWithoutSharedGameId_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Test Game")
            // No SharedGameId set
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new PublishGameCommand(
            GameId: gameId,
            Status: ApprovalStatus.Approved);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Cannot approve game without linking to SharedGameCatalog first", exception.Message);

        // Verify changes were NOT saved
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithDraftStatus_ClearsPublicationFields()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Test Game")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new PublishGameCommand(
            GameId: gameId,
            Status: ApprovalStatus.Draft);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsPublished);
        Assert.Equal(ApprovalStatus.Draft.ToString(), result.ApprovalStatus);
        Assert.Null(result.PublishedAt);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Test Game")
            .WithSharedGameLink(sharedGameId)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new PublishGameCommand(
            GameId: gameId,
            Status: ApprovalStatus.Approved);

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, cancellationToken),
            Times.Once);
        _gameRepositoryMock.Verify(
            r => r.UpdateAsync(game, cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}
