using Api.BoundedContexts.KnowledgeBase.Domain.Events;
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
/// Unit tests for <see cref="AgentDefinitionChangedForCatalogAggregatesHandler"/>.
/// Issue #593 (Wave A.3a) — spec §6.5.
/// Uses raw <see cref="HybridCache"/> (not <c>IHybridCacheService</c>) so that
/// tag invalidation hits the same native tag index populated by the producer
/// <c>SearchSharedGamesQueryHandler</c> (mirrors legacy <see cref="VectorDocumentIndexedForKbFlagHandler"/>).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AgentDefinitionChangedForCatalogAggregatesHandlerTests
{
    private readonly Mock<ILogger<AgentDefinitionChangedForCatalogAggregatesHandler>> _loggerMock = new();

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private AgentDefinitionChangedForCatalogAggregatesHandler CreateHandler(HybridCache cache) =>
        new(cache, _loggerMock.Object);

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentDefinitionChangedForCatalogAggregatesHandler(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentDefinitionChangedForCatalogAggregatesHandler(CreateHybridCache(), null!));
    }

    [Fact]
    public async Task Handle_AgentDefinitionCreatedEvent_InvalidatesSearchGamesTag()
    {
        var cache = CreateHybridCache();

        // Pre-populate cache entry tagged "search-games" so we can verify eviction.
        await cache.GetOrCreateAsync<string>(
            key: "test-key",
            factory: _ => ValueTask.FromResult("seed"),
            options: null,
            tags: new[] { "search-games" },
            cancellationToken: CancellationToken.None);

        var notification = new AgentDefinitionCreatedEvent(
            agentDefinitionId: Guid.NewGuid(),
            name: "Rules Agent");

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
    public async Task Handle_AgentDefinitionUpdatedEvent_InvalidatesSearchGamesTag()
    {
        var cache = CreateHybridCache();
        var notification = new AgentDefinitionUpdatedEvent(
            agentDefinitionId: Guid.NewGuid(),
            changeDescription: "Prompt updated");

        var act = async () => await CreateHandler(cache).Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_AgentDefinitionActivatedEvent_InvalidatesSearchGamesTag()
    {
        var cache = CreateHybridCache();
        var notification = new AgentDefinitionActivatedEvent(agentDefinitionId: Guid.NewGuid());

        var act = async () => await CreateHandler(cache).Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_AgentDefinitionDeactivatedEvent_InvalidatesSearchGamesTag()
    {
        var cache = CreateHybridCache();
        var notification = new AgentDefinitionDeactivatedEvent(agentDefinitionId: Guid.NewGuid());

        var act = async () => await CreateHandler(cache).Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_NullAgentDefinitionCreatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler(CreateHybridCache()).Handle((AgentDefinitionCreatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullAgentDefinitionUpdatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler(CreateHybridCache()).Handle((AgentDefinitionUpdatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullAgentDefinitionActivatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler(CreateHybridCache()).Handle((AgentDefinitionActivatedEvent)null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullAgentDefinitionDeactivatedEvent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CreateHandler(CreateHybridCache()).Handle((AgentDefinitionDeactivatedEvent)null!, CancellationToken.None));
    }
}
