using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class VectorDocumentIndexedForKbFlagHandlerTests
{
    private readonly Mock<ILogger<VectorDocumentIndexedForKbFlagHandler>> _logger = new();

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
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

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateHybridCache(), new PassthroughRetryPolicy(), _logger.Object);
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

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateHybridCache(), new PassthroughRetryPolicy(), _logger.Object);
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
        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateHybridCache(), new PassthroughRetryPolicy(), _logger.Object);
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

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateHybridCache(), new PassthroughRetryPolicy(), _logger.Object);
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
        var handler = new VectorDocumentIndexedForKbFlagHandler(db, CreateHybridCache(), new PassthroughRetryPolicy(), _logger.Object);

        // Act
        var act = async () => await handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithValidUpdate_InvalidatesSearchGamesCache()
    {
        // Arrange — prime the cache with a dummy entry under the "search-games" tag
        var cache = CreateHybridCache();
        var sentinelKey = $"search-games:{Guid.NewGuid()}";
        var tags = new[] { "search-games" };
        await cache.SetAsync(sentinelKey, "cached-value", tags: tags);

        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: false));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, cache, new PassthroughRetryPolicy(), _logger.Object);
        var evt = new VectorDocumentIndexedEvent(
            documentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            chunkCount: 42,
            sharedGameId: sharedGameId);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — SharedGame flag flipped and cache entry evicted
        (await db.SharedGames.FindAsync(sharedGameId))!.HasKnowledgeBase.Should().BeTrue();
        // The cached value should be gone now; GetOrCreateAsync would repopulate.
        // We cannot read the internal HybridCache state directly, but a subsequent
        // write with the same key should succeed and the factory should be invoked
        // (indicating the prior entry was evicted).
        var factoryInvoked = false;
        await cache.GetOrCreateAsync(
            sentinelKey,
            _ =>
            {
                factoryInvoked = true;
                return ValueTask.FromResult("fresh-value");
            },
            tags: tags);
        factoryInvoked.Should().BeTrue("RemoveByTagAsync should have evicted the sentinel");
    }

    [Fact]
    public async Task Handle_WithValidUpdate_InvalidatesPerGameDetailCache()
    {
        // Issue #603 (Wave A.4) — verify the handler also evicts the per-game
        // detail cache `shared-game:{id}` so the next /shared-games/{id} read
        // sees the refreshed HasKnowledgeBase flag and KB previews.
        // Mirrors the search-games eviction test pattern above.
        var cache = CreateHybridCache();
        var sharedGameId = Guid.NewGuid();
        var perGameTag = $"shared-game:{sharedGameId}";
        var detailKey = $"shared-game:{sharedGameId}";

        await cache.SetAsync(detailKey, "seed-detail", tags: new[] { perGameTag });

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: false));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, cache, new PassthroughRetryPolicy(), _logger.Object);
        var evt = new VectorDocumentIndexedEvent(
            documentId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            chunkCount: 42,
            sharedGameId: sharedGameId);

        await handler.Handle(evt, CancellationToken.None);

        var factoryInvoked = false;
        await cache.GetOrCreateAsync(
            detailKey,
            _ =>
            {
                factoryInvoked = true;
                return ValueTask.FromResult("fresh-detail");
            },
            tags: new[] { perGameTag });
        factoryInvoked.Should().BeTrue("RemoveByTagAsync(\"shared-game:{id}\") should have evicted the detail entry");
    }
}
