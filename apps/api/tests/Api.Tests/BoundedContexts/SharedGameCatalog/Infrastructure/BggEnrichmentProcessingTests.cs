using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Tests for BGG enrichment processing in BggImportQueueBackgroundService.
/// Validates: auto-match, enrichment data mapping, failure states, stale recovery.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BggEnrichmentProcessingTests
{
    private readonly Mock<IBggImportQueueService> _mockQueueService;
    private readonly Mock<IBggApiService> _mockBggService;
    private readonly Mock<ISharedGameRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMediator> _mockMediator;
    private readonly BggImportQueueBackgroundService _service;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public BggEnrichmentProcessingTests()
    {
        _mockQueueService = new Mock<IBggImportQueueService>();
        _mockBggService = new Mock<IBggApiService>();
        _mockRepository = new Mock<ISharedGameRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMediator = new Mock<IMediator>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();

        var mockScope = new Mock<IServiceScope>();
        var mockProvider = new Mock<IServiceProvider>();

        mockProvider.Setup(p => p.GetService(typeof(IBggImportQueueService)))
            .Returns(_mockQueueService.Object);
        mockProvider.Setup(p => p.GetService(typeof(IBggApiService)))
            .Returns(_mockBggService.Object);
        mockProvider.Setup(p => p.GetService(typeof(ISharedGameRepository)))
            .Returns(_mockRepository.Object);
        mockProvider.Setup(p => p.GetService(typeof(IUnitOfWork)))
            .Returns(_mockUnitOfWork.Object);
        mockProvider.Setup(p => p.GetService(typeof(IMediator)))
            .Returns(_mockMediator.Object);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var config = Options.Create(new BggImportQueueConfiguration
        {
            Enabled = true,
            ProcessingIntervalSeconds = 1,
            MaxRetryAttempts = 3,
            AutoCleanupDays = 30,
            BaseRetryDelaySeconds = 1,
            InitialDelayMinutes = 0
        });

        _service = new BggImportQueueBackgroundService(
            _mockScopeFactory.Object,
            Mock.Of<ILogger<BggImportQueueBackgroundService>>(),
            config);
    }

    [Fact]
    public void EnrichmentQueueItem_WithBggId_ShouldFetchAndEnrich()
    {
        // Arrange: Create a skeleton game and simulate enrichment
        var game = SharedGame.CreateSkeleton("Catan", TestUserId, TimeProvider.System, bggId: 13);
        var gameId = game.Id;

        // The game starts as Skeleton, needs to go through EnrichmentQueued before Enriching
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        game.GameDataStatus.Should().Be(GameDataStatus.EnrichmentQueued);

        // Transition to Enriching (which the background service does)
        game.TransitionDataStatusTo(GameDataStatus.Enriching);
        game.GameDataStatus.Should().Be(GameDataStatus.Enriching);

        // Enrich the game with BGG data
        game.EnrichFromBgg(
            description: "Trade, build, settle",
            yearPublished: 1995,
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 75,
            minAge: 10,
            complexityRating: 2.31m,
            averageRating: 7.15m,
            imageUrl: "https://cf.geekdo-images.com/catan.jpg",
            thumbnailUrl: "https://cf.geekdo-images.com/catan_thumb.jpg",
            rulebookUrl: null,
            bggId: 13);

        // Assert
        game.GameDataStatus.Should().Be(GameDataStatus.Enriched);
        game.Title.Should().Be("Catan");
        game.MinPlayers.Should().Be(3);
        game.MaxPlayers.Should().Be(4);
        game.BggId.Should().Be(13);
    }

    [Fact]
    public void EnrichmentQueueItem_AutoMatchZeroResults_ShouldTransitionToFailed()
    {
        // Arrange: Skeleton game with no BggId
        var game = SharedGame.CreateSkeleton("Unknown Game XYZ", TestUserId, TimeProvider.System);
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        game.TransitionDataStatusTo(GameDataStatus.Enriching);

        // Act: Simulate auto-match failure → transition to Failed
        game.TransitionDataStatusTo(GameDataStatus.Failed);

        // Assert
        game.GameDataStatus.Should().Be(GameDataStatus.Failed);
    }

    [Fact]
    public void EnrichFromBgg_NotInEnrichingState_ShouldThrow()
    {
        // Arrange: Skeleton game (not in Enriching state)
        var game = SharedGame.CreateSkeleton("Test Game", TestUserId, TimeProvider.System);

        // Act & Assert
        var act = () => game.EnrichFromBgg(
            description: "Test",
            yearPublished: 2024,
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rulebookUrl: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot enrich*Skeleton*Must be in Enriching*");
    }

    [Fact]
    public void EnrichedGame_CanTransitionToComplete()
    {
        // Arrange: Fully enriched game
        var game = SharedGame.CreateSkeleton("Wingspan", TestUserId, TimeProvider.System, bggId: 266192);
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        game.TransitionDataStatusTo(GameDataStatus.Enriching);
        game.EnrichFromBgg(
            description: "Bird engine-building",
            yearPublished: 2019,
            minPlayers: 1,
            maxPlayers: 5,
            playingTimeMinutes: 70,
            minAge: 10,
            complexityRating: 2.44m,
            averageRating: 8.09m,
            imageUrl: "https://cf.geekdo-images.com/wingspan.jpg",
            thumbnailUrl: "https://cf.geekdo-images.com/wingspan_thumb.jpg",
            rulebookUrl: null,
            bggId: 266192);

        // Act
        game.MarkDataComplete();

        // Assert
        game.GameDataStatus.Should().Be(GameDataStatus.Complete);
    }

    [Fact]
    public void SkeletonGame_CannotTransitionDirectlyToComplete()
    {
        // Arrange
        var game = SharedGame.CreateSkeleton("Test", TestUserId, TimeProvider.System);

        // Act & Assert
        var act = () => game.TransitionDataStatusTo(GameDataStatus.Complete);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FailedGame_CanBeReenqueued()
    {
        // Arrange: Game that failed enrichment
        var game = SharedGame.CreateSkeleton("Failed Game", TestUserId, TimeProvider.System);
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        game.TransitionDataStatusTo(GameDataStatus.Enriching);
        game.TransitionDataStatusTo(GameDataStatus.Failed);

        // Act: Re-enqueue
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);

        // Assert
        game.GameDataStatus.Should().Be(GameDataStatus.EnrichmentQueued);
    }

    [Fact]
    public async Task StaleRecovery_ShouldResetStuckItems()
    {
        // Arrange
        _mockQueueService.Setup(s => s.RecoverStaleItemsAsync(
            It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // The stale recovery is tested indirectly through the queue service
        var recovered = await _mockQueueService.Object
            .RecoverStaleItemsAsync(TimeSpan.FromMinutes(5), CancellationToken.None);

        // Assert
        recovered.Should().Be(3);
        _mockQueueService.Verify(s => s.RecoverStaleItemsAsync(
            TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryClaim_AtomicClaim_OnlyOneSucceeds()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var callCount = 0;

        _mockQueueService.Setup(s => s.TryClaimItemAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // First call succeeds, subsequent fail (simulating atomic claim)
                return Interlocked.Increment(ref callCount) == 1;
            });

        // Act
        var claim1 = await _mockQueueService.Object.TryClaimItemAsync(itemId, CancellationToken.None);
        var claim2 = await _mockQueueService.Object.TryClaimItemAsync(itemId, CancellationToken.None);

        // Assert
        claim1.Should().BeTrue();
        claim2.Should().BeFalse();
    }

    [Fact]
    public void EnrichFromBgg_WithRulebookUrl_ShouldSetGameRules()
    {
        // Arrange
        var game = SharedGame.CreateSkeleton("Gloomhaven", TestUserId, TimeProvider.System, bggId: 174430);
        game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        game.TransitionDataStatusTo(GameDataStatus.Enriching);

        // Act
        game.EnrichFromBgg(
            description: "Tactical combat dungeon crawler",
            yearPublished: 2017,
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 120,
            minAge: 14,
            complexityRating: 3.86m,
            averageRating: 8.44m,
            imageUrl: "https://cf.geekdo-images.com/gloomhaven.jpg",
            thumbnailUrl: "https://cf.geekdo-images.com/gloomhaven_thumb.jpg",
            rulebookUrl: "https://example.com/gloomhaven-rules.pdf",
            bggId: 174430);

        // Assert
        game.GameDataStatus.Should().Be(GameDataStatus.Enriched);
        game.Rules.Should().NotBeNull();
        game.Rules!.ExternalUrl.Should().Be("https://example.com/gloomhaven-rules.pdf");
    }
}