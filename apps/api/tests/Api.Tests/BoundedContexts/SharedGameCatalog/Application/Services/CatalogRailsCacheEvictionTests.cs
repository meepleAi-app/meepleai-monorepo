using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #4659 — le rail del catalogo (<c>newGames</c>, <c>trending</c>) seguono lo stato dei giochi: un gioco
/// archiviato o cancellato ne esce, uno pubblicato ci entra, appena l'invalidatore toglie la lista.
/// </summary>
/// <remarks>
/// <para>
/// Qui la cache è VERA: <see cref="HybridCache"/> in memoria, il wrapper <see cref="HybridCacheService"/> con
/// cui le rail scrivono le voci, la retry policy e <see cref="SharedGameCacheInvalidator"/> veri. Gli altri test
/// delle rail usano un doppio che chiama sempre la factory, e l'host di integrazione sostituisce il wrapper con
/// un passthrough (<c>TestHybridCacheService</c>): né l'uno né l'altro potrebbe mostrare una voce vecchia.
/// </para>
/// <para>
/// Ogni test prima cambia il database SENZA evincere e verifica che la rail serva ancora la voce vecchia: è la
/// prova che la cache c'è e che il test non passa per niente. Solo dopo chiama l'invalidatore.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4659")]
public sealed class CatalogRailsCacheEvictionTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly MeepleAiDbContext _db;
    private readonly ISharedGameCacheInvalidator _invalidator;
    private readonly GetNewGamesQueryHandler _newGames;
    private readonly GetCatalogTrendingQueryHandler _trending;

    public CatalogRailsCacheEvictionTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        _provider = services.BuildServiceProvider();
        var hybridCache = _provider.GetRequiredService<HybridCache>();

        // Il wrapper con cui le rail scrivono le voci, senza Redis: la lista e il dettaglio sono scritti con
        // HybridCache grezzo, le rail con questo.
        var wrapper = new HybridCacheService(
            hybridCache,
            Options.Create(new HybridCacheConfiguration()),
            NullLogger<HybridCacheService>.Instance);
        _invalidator = new SharedGameCacheInvalidator(
            wrapper,
            new CacheInvalidationRetryPolicy(NullLogger<CacheInvalidationRetryPolicy>.Instance),
            hybridCache);

        _db = TestDbContextFactory.CreateInMemoryDbContext();
        var blobStorage = new Mock<IBlobStorageService>().Object;
        _newGames = new GetNewGamesQueryHandler(_db, wrapper, blobStorage, NullLogger<GetNewGamesQueryHandler>.Instance);
        _trending = new GetCatalogTrendingQueryHandler(_db, wrapper, blobStorage, NullLogger<GetCatalogTrendingQueryHandler>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _provider.Dispose();
    }

    private async Task<SharedGameEntity> SeedGameAsync(string title, GameStatus status = GameStatus.Published, bool withPlay = false)
    {
        var entity = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            YearPublished = 2024,
            Description = "Test game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = (int)status,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };
        _db.SharedGames.Add(entity);
        if (withPlay)
        {
            _db.Set<GameAnalyticsEventEntity>().Add(new GameAnalyticsEventEntity
            {
                Id = Guid.NewGuid(),
                GameId = entity.Id,
                EventType = (int)GameEventType.Play,
                UserId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddHours(-1),
            });
        }
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }

    private async Task SetStateAsync(SharedGameEntity game, GameStatus status, bool isDeleted = false)
    {
        game.Status = (int)status;
        game.IsDeleted = isDeleted;
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string[]> NewGameNamesAsync() =>
        (await _newGames.Handle(new GetNewGamesQuery(Limit: 10), TestContext.Current.CancellationToken))
        .Select(g => g.Name).Order(StringComparer.Ordinal).ToArray();

    private async Task<string[]> TrendingTitlesAsync() =>
        (await _trending.Handle(new GetCatalogTrendingQuery { Limit = 10 }, TestContext.Current.CancellationToken))
        .Select(g => g.Title).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task NewGames_ArchivedGame_LeavesTheRail_OnceTheListIsEvicted()
    {
        var kept = await SeedGameAsync("Kept");
        var archived = await SeedGameAsync("Archived later");
        (await NewGameNamesAsync()).Should().Equal("Archived later", "Kept");

        await SetStateAsync(archived, GameStatus.Archived);
        (await NewGameNamesAsync()).Should().Equal(
            new[] { "Archived later", "Kept" },
            "nothing has evicted yet: the cached rail is what the 1h entry holds");

        await _invalidator.EvictDetailAndListAsync(archived.Id, TestContext.Current.CancellationToken);

        (await NewGameNamesAsync()).Should().Equal(kept.Title);
    }

    [Fact]
    public async Task NewGames_DeletedGame_LeavesTheRail_OnceTheListIsEvicted()
    {
        await SeedGameAsync("Kept");
        var deleted = await SeedGameAsync("Deleted later");
        (await NewGameNamesAsync()).Should().Equal("Deleted later", "Kept");

        await SetStateAsync(deleted, GameStatus.Published, isDeleted: true);
        (await NewGameNamesAsync()).Should().Contain("Deleted later", "the cached rail is still served");

        await _invalidator.EvictDetailAndListAsync(deleted.Id, TestContext.Current.CancellationToken);

        (await NewGameNamesAsync()).Should().Equal("Kept");
    }

    [Fact]
    public async Task NewGames_PublishedGame_EntersTheRail_OnceTheBatchEvictionRuns()
    {
        var draft = await SeedGameAsync("Draft then published", GameStatus.Draft);
        (await NewGameNamesAsync()).Should().BeEmpty();

        await SetStateAsync(draft, GameStatus.Published);
        (await NewGameNamesAsync()).Should().BeEmpty("the empty rail is cached too");

        await _invalidator.EvictDetailsAndListAsync([draft.Id], TestContext.Current.CancellationToken);

        (await NewGameNamesAsync()).Should().Equal("Draft then published");
    }

    [Fact]
    public async Task Trending_ArchivedGame_LeavesTheRail_OnceTheListIsEvicted()
    {
        var kept = await SeedGameAsync("Kept", withPlay: true);
        var archived = await SeedGameAsync("Archived later", withPlay: true);
        (await TrendingTitlesAsync()).Should().Equal("Archived later", "Kept");

        await SetStateAsync(archived, GameStatus.Archived);
        (await TrendingTitlesAsync()).Should().Equal(
            new[] { "Archived later", "Kept" },
            "nothing has evicted yet: the cached rail is what the 12h entry holds");

        await _invalidator.EvictDetailAndListAsync(archived.Id, TestContext.Current.CancellationToken);

        (await TrendingTitlesAsync()).Should().Equal(kept.Title);
    }

    [Fact]
    public async Task Trending_DeletedGame_LeavesTheRail_OnceTheListIsEvicted()
    {
        await SeedGameAsync("Kept", withPlay: true);
        var deleted = await SeedGameAsync("Deleted later", withPlay: true);
        (await TrendingTitlesAsync()).Should().Equal("Deleted later", "Kept");

        await SetStateAsync(deleted, GameStatus.Published, isDeleted: true);
        (await TrendingTitlesAsync()).Should().Contain("Deleted later", "the cached rail is still served");

        await _invalidator.EvictDetailsAndListAsync([deleted.Id], TestContext.Current.CancellationToken);

        (await TrendingTitlesAsync()).Should().Equal("Kept");
    }

    /// <summary>
    /// Errata e FAQ toccano solo il dettaglio: le rail non le riportano, e non c'è motivo di far ricalcolare
    /// la classifica del trending (una scansione di sette giorni di eventi).
    /// </summary>
    [Fact]
    public async Task EvictingOnlyTheDetail_LeavesTheRailsCached()
    {
        var game = await SeedGameAsync("Stays cached", withPlay: true);
        (await NewGameNamesAsync()).Should().Equal("Stays cached");
        (await TrendingTitlesAsync()).Should().Equal("Stays cached");

        await SetStateAsync(game, GameStatus.Archived);
        await _invalidator.EvictDetailAsync(game.Id, TestContext.Current.CancellationToken);

        (await NewGameNamesAsync()).Should().Equal("Stays cached");
        (await TrendingTitlesAsync()).Should().Equal("Stays cached");
    }
}
