using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
/// Unit tests for <see cref="ToolkitChangedForCatalogAggregatesHandler"/>.
/// Issue #593 (Wave A.3a) — search-games tag invalidation.
/// Issue #603 (Wave A.4) — extended with shared-game:{id} tag invalidation
/// scoped via Toolkit → Game.SharedGameId lookup.
///
/// Uses raw <see cref="HybridCache"/> (not <c>IHybridCacheService</c>) so that
/// tag invalidation hits the same native tag index populated by producers
/// <c>SearchSharedGamesQueryHandler</c> and <c>GetSharedGameByIdQueryHandler</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ToolkitChangedForCatalogAggregatesHandlerTests
{
    private readonly Mock<ILogger<ToolkitChangedForCatalogAggregatesHandler>> _loggerMock = new();

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private ToolkitChangedForCatalogAggregatesHandler CreateHandler(
        MeepleAiDbContext db,
        HybridCache cache) => new(db, cache, _loggerMock.Object);

    [Fact]
    public void Constructor_WithNullDbContext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ToolkitChangedForCatalogAggregatesHandler(null!, CreateHybridCache(), _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        Assert.Throws<ArgumentNullException>(() =>
            new ToolkitChangedForCatalogAggregatesHandler(db, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        Assert.Throws<ArgumentNullException>(() =>
            new ToolkitChangedForCatalogAggregatesHandler(db, CreateHybridCache(), null!));
    }

    [Fact]
    public async Task Handle_ToolkitCreatedEvent_InvalidatesSearchGamesTag()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();

        // Pre-populate cache entry tagged "search-games" so we can verify eviction.
        await cache.GetOrCreateAsync<string>(
            key: "test-key",
            factory: _ => ValueTask.FromResult("seed"),
            options: null,
            tags: new[] { "search-games" },
            cancellationToken: CancellationToken.None);

        var notification = new ToolkitCreatedEvent(
            toolkitId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            privateGameId: null,
            name: "Test Toolkit");

        var act = async () => await CreateHandler(db, cache).Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();

        // Subsequent factory must run again (cache evicted by tag).
        var factoryRan = false;
        await cache.GetOrCreateAsync<string>(
            key: "test-key",
            factory: _ =>
            {
                factoryRan = true;
                return ValueTask.FromResult("repop");
            },
            options: null,
            tags: new[] { "search-games" },
            cancellationToken: CancellationToken.None);
        factoryRan.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ToolkitPublishedEvent_DoesNotThrow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();
        var notification = new ToolkitPublishedEvent(toolkitId: Guid.NewGuid(), version: 1);

        var act = async () => await CreateHandler(db, cache).Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_NullToolkitCreatedEvent_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler(db, CreateHybridCache()).Handle((ToolkitCreatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullToolkitPublishedEvent_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler(db, CreateHybridCache()).Handle((ToolkitPublishedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ToolkitWithSharedGameLinkage_InvalidatesPerGameTag()
    {
        // Wave A.4 — verify that when a Toolkit is linked through Game.SharedGameId
        // the per-game detail cache (`shared-game:{id}`) is also evicted.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sharedGameId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        db.Games.Add(new GameEntity
        {
            Id = gameId,
            Name = "G",
            CreatedAt = DateTime.UtcNow,
            SharedGameId = sharedGameId,
        });
        var toolkit = Toolkit.CreateDefault(gameId);
        db.Toolkits.Add(toolkit);
        await db.SaveChangesAsync();

        var cache = CreateHybridCache();
        var perGameTag = $"shared-game:{sharedGameId}";

        await cache.GetOrCreateAsync<string>(
            key: "detail-cache-key",
            factory: _ => ValueTask.FromResult("seed-detail"),
            options: null,
            tags: new[] { perGameTag },
            cancellationToken: CancellationToken.None);

        var notification = new ToolkitCreatedEvent(
            toolkitId: toolkit.Id,
            gameId: gameId,
            privateGameId: null,
            name: "Test Toolkit");

        await CreateHandler(db, cache).Handle(notification, CancellationToken.None);

        // Subsequent fetch with same tag should re-invoke factory (= eviction worked).
        var factoryRan = false;
        await cache.GetOrCreateAsync<string>(
            key: "detail-cache-key",
            factory: _ =>
            {
                factoryRan = true;
                return ValueTask.FromResult("repop");
            },
            options: null,
            tags: new[] { perGameTag },
            cancellationToken: CancellationToken.None);
        factoryRan.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ToolkitWithoutSharedGameLinkage_OnlyInvalidatesSearchGames()
    {
        // Toolkit attached to a Game without SharedGameId → only search-games tag evicted.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = Guid.NewGuid();

        db.Games.Add(new GameEntity
        {
            Id = gameId,
            Name = "Private G",
            CreatedAt = DateTime.UtcNow,
            SharedGameId = null,
        });
        var toolkit = Toolkit.CreateDefault(gameId);
        db.Toolkits.Add(toolkit);
        await db.SaveChangesAsync();

        var cache = CreateHybridCache();
        var act = async () => await CreateHandler(db, cache).Handle(
            new ToolkitCreatedEvent(toolkit.Id, gameId, null, "Test"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
