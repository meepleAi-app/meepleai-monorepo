using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Unit tests for EnqueueEnrichmentCommandHandler, EnqueueAllSkeletonsCommandHandler,
/// and MarkGamesCompleteCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class EnqueueEnrichmentCommandTests
{
    private readonly Mock<ISharedGameRepository> _mockRepository;
    private readonly Mock<IBggImportQueueService> _mockQueueService;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<EnqueueEnrichmentCommandHandler>> _mockEnqueueLogger;
    private readonly Mock<ILogger<EnqueueAllSkeletonsCommandHandler>> _mockAllSkeletonsLogger;
    private readonly Mock<ILogger<MarkGamesCompleteCommandHandler>> _mockCompleteLogger;

    private readonly EnqueueEnrichmentCommandHandler _enqueueHandler;
    private readonly EnqueueAllSkeletonsCommandHandler _allSkeletonsHandler;
    private readonly MarkGamesCompleteCommandHandler _completeHandler;

    private static readonly Guid AdminUserId = Guid.NewGuid();

    public EnqueueEnrichmentCommandTests()
    {
        _mockRepository = new Mock<ISharedGameRepository>();
        _mockQueueService = new Mock<IBggImportQueueService>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockEnqueueLogger = new Mock<ILogger<EnqueueEnrichmentCommandHandler>>();
        _mockAllSkeletonsLogger = new Mock<ILogger<EnqueueAllSkeletonsCommandHandler>>();
        _mockCompleteLogger = new Mock<ILogger<MarkGamesCompleteCommandHandler>>();

        _enqueueHandler = new EnqueueEnrichmentCommandHandler(
            _mockRepository.Object,
            _mockQueueService.Object,
            _mockUnitOfWork.Object,
            _mockEnqueueLogger.Object);

        _allSkeletonsHandler = new EnqueueAllSkeletonsCommandHandler(
            _mockRepository.Object,
            _mockQueueService.Object,
            _mockUnitOfWork.Object,
            _mockAllSkeletonsLogger.Object);

        _completeHandler = new MarkGamesCompleteCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockCompleteLogger.Object);
    }

    // ── EnqueueEnrichmentCommand ────────────────────────────────────

    [Fact]
    public async Task EnqueueEnrichment_SkeletonGame_ShouldEnqueue()
    {
        // Arrange
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider.System, bggId: 13);
        var gameId = game.Id;

        _mockRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame> { [gameId] = game });

        _mockQueueService
            .Setup(q => q.EnqueueEnrichmentBatchAsync(
                It.IsAny<IEnumerable<(Guid, int?, string)>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid.NewGuid(), 1));

        var command = new EnqueueEnrichmentCommand([gameId], AdminUserId);

        // Act
        var result = await _enqueueHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Enqueued.Should().Be(1);
        result.Skipped.Should().Be(0);
        game.GameDataStatus.Should().Be(GameDataStatus.EnrichmentQueued);

        _mockQueueService.Verify(
            q => q.EnqueueEnrichmentBatchAsync(
                It.Is<IEnumerable<(Guid, int?, string)>>(items =>
                    items.Count() == 1),
                AdminUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueEnrichment_GameAlreadyQueued_ShouldSkip()
    {
        // Arrange
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider.System, bggId: 13);
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued); // already queued
        var gameId = game.Id;

        _mockRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame> { [gameId] = game });

        var command = new EnqueueEnrichmentCommand([gameId], AdminUserId);

        // Act
        var result = await _enqueueHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Enqueued.Should().Be(0);
        result.Skipped.Should().Be(1);

        _mockQueueService.Verify(
            q => q.EnqueueEnrichmentBatchAsync(
                It.IsAny<IEnumerable<(Guid, int?, string)>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueEnrichment_FailedGame_ShouldReenqueue()
    {
        // Arrange — create a game that has gone through Skeleton → Queued → Enriching → Failed
        var game = SharedGame.CreateSkeleton("Wingspan", AdminUserId, TimeProvider.System, bggId: 266192);
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        game.TransitionDataStatusTo(GameDataStatus.Enriching);
        game.TransitionDataStatusTo(GameDataStatus.Failed);
        var gameId = game.Id;

        _mockRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame> { [gameId] = game });

        _mockQueueService
            .Setup(q => q.EnqueueEnrichmentBatchAsync(
                It.IsAny<IEnumerable<(Guid, int?, string)>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid.NewGuid(), 1));

        var command = new EnqueueEnrichmentCommand([gameId], AdminUserId);

        // Act
        var result = await _enqueueHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Enqueued.Should().Be(1);
        result.Skipped.Should().Be(0);
        game.GameDataStatus.Should().Be(GameDataStatus.EnrichmentQueued);

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── EnqueueAllSkeletonsCommand ──────────────────────────────────

    [Fact]
    public async Task EnqueueAllSkeletons_ShouldEnqueueAllSkeletonsAndFailed()
    {
        // Arrange
        var skeleton1 = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider.System, bggId: 13);
        var skeleton2 = SharedGame.CreateSkeleton("Azul", AdminUserId, TimeProvider.System, bggId: 230802);

        var failedGame = SharedGame.CreateSkeleton("Wingspan", AdminUserId, TimeProvider.System, bggId: 266192);
        failedGame.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        failedGame.TransitionDataStatusTo(GameDataStatus.Enriching);
        failedGame.TransitionDataStatusTo(GameDataStatus.Failed);

        _mockRepository
            .Setup(r => r.GetByGameDataStatusAsync(GameDataStatus.Skeleton, It.IsAny<CancellationToken>()))
            .ReturnsAsync([skeleton1, skeleton2]);

        _mockRepository
            .Setup(r => r.GetByGameDataStatusAsync(GameDataStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync([failedGame]);

        _mockQueueService
            .Setup(q => q.EnqueueEnrichmentBatchAsync(
                It.IsAny<IEnumerable<(Guid, int?, string)>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid.NewGuid(), 3));

        var command = new EnqueueAllSkeletonsCommand(AdminUserId);

        // Act
        var result = await _allSkeletonsHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Enqueued.Should().Be(3);
        result.Skipped.Should().Be(0);

        skeleton1.GameDataStatus.Should().Be(GameDataStatus.EnrichmentQueued);
        skeleton2.GameDataStatus.Should().Be(GameDataStatus.EnrichmentQueued);
        failedGame.GameDataStatus.Should().Be(GameDataStatus.EnrichmentQueued);

        _mockQueueService.Verify(
            q => q.EnqueueEnrichmentBatchAsync(
                It.Is<IEnumerable<(Guid, int?, string)>>(items =>
                    items.Count() == 3),
                AdminUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── MarkGamesCompleteCommand ────────────────────────────────────

    [Fact]
    public async Task MarkGamesComplete_EnrichedGame_ShouldTransition()
    {
        // Arrange — create a game that has reached Enriched status
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider.System, bggId: 13);
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        game.TransitionDataStatusTo(GameDataStatus.Enriching);
        game.TransitionDataStatusTo(GameDataStatus.Enriched);
        var gameId = game.Id;

        _mockRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame> { [gameId] = game });

        var command = new MarkGamesCompleteCommand([gameId]);

        // Act
        var result = await _completeHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(1);
        game.GameDataStatus.Should().Be(GameDataStatus.Complete);

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkGamesComplete_SkeletonGame_ShouldSkip()
    {
        // Arrange — game is still Skeleton, not eligible for MarkDataComplete
        var game = SharedGame.CreateSkeleton("Azul", AdminUserId, TimeProvider.System, bggId: 230802);
        var gameId = game.Id;

        _mockRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SharedGame> { [gameId] = game });

        var command = new MarkGamesCompleteCommand([gameId]);

        // Act
        var result = await _completeHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(0);
        game.GameDataStatus.Should().Be(GameDataStatus.Skeleton);

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
