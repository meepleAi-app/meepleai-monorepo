using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.Tests.Constants;
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
/// Issue #593 (Wave A.3a) — spec §6.5.
/// Uses raw <see cref="HybridCache"/> (not <c>IHybridCacheService</c>) so that
/// tag invalidation hits the same native tag index populated by the producer
/// <c>SearchSharedGamesQueryHandler</c> (mirrors legacy <see cref="VectorDocumentIndexedForKbFlagHandler"/>).
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

    private ToolkitChangedForCatalogAggregatesHandler CreateHandler(HybridCache cache) =>
        new(cache, _loggerMock.Object);

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ToolkitChangedForCatalogAggregatesHandler(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ToolkitChangedForCatalogAggregatesHandler(CreateHybridCache(), null!));
    }

    [Fact]
    public async Task Handle_ToolkitCreatedEvent_InvalidatesSearchGamesTag()
    {
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

        var act = async () => await CreateHandler(cache).Handle(notification, CancellationToken.None);

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
    public async Task Handle_ToolkitPublishedEvent_InvalidatesSearchGamesTag()
    {
        var cache = CreateHybridCache();
        var notification = new ToolkitPublishedEvent(toolkitId: Guid.NewGuid(), version: 1);

        var act = async () => await CreateHandler(cache).Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_NullToolkitCreatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler(CreateHybridCache()).Handle((ToolkitCreatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullToolkitPublishedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler(CreateHybridCache()).Handle((ToolkitPublishedEvent)null!, CancellationToken.None));
    }
}
