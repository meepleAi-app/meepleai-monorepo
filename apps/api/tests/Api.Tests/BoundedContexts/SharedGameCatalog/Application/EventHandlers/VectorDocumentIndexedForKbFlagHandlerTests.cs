using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Unit tests for VectorDocumentIndexedForKbFlagHandler.
/// S2 of library-to-game epic — maintains the denormalized
/// <c>has_knowledge_base</c> column on <c>shared_games</c> in response to
/// VectorDocumentIndexedEvent notifications from the KnowledgeBase BC.
///
/// Tech debt revision (CR-I1, CR-M4):
///   - Event carries SharedGameId directly (no cross-BC DB read).
///   - Handler invalidates the HybridCache tag "search-games" after updates.
///
/// Epic #2242 Sub #6 Block C (ADR-062):
///   - Cache eviction now goes through IHybridCacheService.RemoveByTagAcrossReplicasAsync
///     for cross-replica L1 invalidation via Redis Pub/Sub.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class VectorDocumentIndexedForKbFlagHandlerTests
{
    private readonly Mock<ILogger<VectorDocumentIndexedForKbFlagHandler>> _logger = new();

    private static Mock<IHybridCacheService> CreateCacheMock()
    {
        var mock = new Mock<IHybridCacheService>(MockBehavior.Strict);
        mock.Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        return mock;
    }

    private static SharedGameEntity CreateSharedGame(Guid id, bool hasKb = false) =>
        new()
        {
            Id = id,
            Title = "Test Game",
            YearPublished = 2020,
            Description = "Desc",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 30,
            MinAge = 8,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            HasKnowledgeBase = hasKb,
        };

    [Fact]
    public async Task Handle_EventWithSharedGameId_FlipsHasKnowledgeBaseToTrue()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: false));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateCacheMock().Object, new PassthroughRetryPolicy(), _logger.Object);
        var evt = new VectorDocumentIndexedEvent(documentId, gameId, chunkCount: 42, sharedGameId: sharedGameId);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        var updated = await db.SharedGames.FindAsync(sharedGameId);
        updated.Should().NotBeNull();
        updated!.HasKnowledgeBase.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EventWithNullSharedGameId_DoesNotUpdateAnything()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: false));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateCacheMock().Object, new PassthroughRetryPolicy(), _logger.Object);
        var evt = new VectorDocumentIndexedEvent(documentId, gameId, chunkCount: 42, sharedGameId: null);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        var shouldNotChange = await db.SharedGames.FindAsync(sharedGameId);
        shouldNotChange!.HasKnowledgeBase.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EventWithUnknownSharedGameId_DoesNotThrow()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateCacheMock().Object, new PassthroughRetryPolicy(), _logger.Object);
        var evt = new VectorDocumentIndexedEvent(
            documentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            chunkCount: 42,
            sharedGameId: Guid.NewGuid()); // exists in event but not in DB

        // Act
        var act = async () => await handler.Handle(evt, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_SharedGameAlreadyHasKnowledgeBase_IsIdempotent()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: true));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateCacheMock().Object, new PassthroughRetryPolicy(), _logger.Object);
        var evt = new VectorDocumentIndexedEvent(documentId, gameId, chunkCount: 42, sharedGameId: sharedGameId);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — still true, no exception
        var stillTrue = await db.SharedGames.FindAsync(sharedGameId);
        stillTrue!.HasKnowledgeBase.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NullNotification_ThrowsArgumentNullException()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateCacheMock().Object, new PassthroughRetryPolicy(), _logger.Object);

        // Act
        var act = async () => await handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithValidUpdate_InvokesCrossReplicaInvalidationForSearchGamesTag()
    {
        // Issue #2242 Sub #6 Block C (ADR-062): handler must invoke the cross-replica
        // wrapper so other replicas evict their L1 entries via Redis Pub/Sub broadcast.
        // The wrapper internally performs local RemoveByTagAsync (L1 + L2) before broadcasting.
        var cacheMock = CreateCacheMock();
        var sharedGameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: false));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, cacheMock.Object, new PassthroughRetryPolicy(), _logger.Object);
        var evt = new VectorDocumentIndexedEvent(
            documentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            chunkCount: 42,
            sharedGameId: sharedGameId);

        await handler.Handle(evt, CancellationToken.None);

        cacheMock.Verify(
            c => c.RemoveByTagAcrossReplicasAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once,
            "handler should evict the catalog list cache namespace across replicas after the flag flip");
    }

    [Fact]
    public async Task Handle_WithValidUpdate_InvokesCrossReplicaInvalidationForPerGameDetailTag()
    {
        // Issue #603 (Wave A.4) — verify the handler also evicts the per-game
        // detail cache `shared-game:{id}` across replicas so the next
        // /shared-games/{id} read on ANY replica sees the refreshed HasKnowledgeBase flag.
        var cacheMock = CreateCacheMock();
        var sharedGameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: false));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, cacheMock.Object, new PassthroughRetryPolicy(), _logger.Object);
        var evt = new VectorDocumentIndexedEvent(
            documentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            chunkCount: 42,
            sharedGameId: sharedGameId);

        await handler.Handle(evt, CancellationToken.None);

        cacheMock.Verify(
            c => c.RemoveByTagAcrossReplicasAsync($"shared-game:{sharedGameId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "handler should evict the per-game detail cache across replicas after the flag flip");
    }
}
