using Api.BoundedContexts.Administration.Application.EventHandlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Configuration;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Issue #3974: Unit tests for UserActivityCacheInvalidationEventHandler.
/// Verifies tag-based cache invalidation for user activity events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UserActivityCacheInvalidationEventHandlerTests
{
    private readonly Mock<HybridCache> _cacheMock;
    private readonly Mock<ILogger<UserActivityCacheInvalidationEventHandler>> _loggerMock;
    private readonly UserActivityCacheInvalidationEventHandler _handler;

    public UserActivityCacheInvalidationEventHandlerTests()
    {
        _cacheMock = new Mock<HybridCache>();
        _loggerMock = new Mock<ILogger<UserActivityCacheInvalidationEventHandler>>();
        _handler = new UserActivityCacheInvalidationEventHandler(_cacheMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_UserLibraryGameAddedEvent_InvalidatesUserCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var evt = new UserLibraryGameAddedEvent(userId, gameId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate cache by user tag when game added to library");
    }

    [Fact]
    public async Task Handle_UserGameSessionCompletedEvent_InvalidatesUserCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var evt = new UserGameSessionCompletedEvent(userId, sessionId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate cache by user tag when game session completed");
    }

    [Fact]
    public async Task Handle_UserChatSavedEvent_InvalidatesUserCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var evt = new UserChatSavedEvent(userId, chatId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate cache by user tag when chat saved");
    }

    [Fact]
    public async Task Handle_UserWishlistUpdatedEvent_InvalidatesUserCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var evt = new UserWishlistUpdatedEvent(userId, gameId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate cache by user tag when wishlist updated");
    }

    [Fact]
    public async Task Handle_SessionFinalizedEvent_InvalidatesActivityTimelineTag()
    {
        // Arrange
        var evt = new SessionFinalizedEvent
        {
            SessionId = Guid.NewGuid(),
            DurationMinutes = 60
        };

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync("activity-timeline", It.IsAny<CancellationToken>()),
            Times.Once,
            "SessionFinalizedEvent should invalidate activity-timeline tag (no UserId available)");
    }

    [Fact]
    public async Task Handle_CacheThrows_LogsErrorButDoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var evt = new UserLibraryGameAddedEvent(userId, Guid.NewGuid());

        _cacheMock
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection failed"));

        // Act
        var exception = await Record.ExceptionAsync(() => _handler.Handle(evt, CancellationToken.None));

        // Assert
        exception.Should().BeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate dashboard cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log error when cache invalidation fails");
    }

    [Fact]
    public async Task Handle_SessionFinalizedCacheThrows_LogsErrorButDoesNotThrow()
    {
        // Arrange
        var evt = new SessionFinalizedEvent
        {
            SessionId = Guid.NewGuid(),
            DurationMinutes = 30
        };

        _cacheMock
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache unavailable"));

        // Act
        var exception = await Record.ExceptionAsync(() => _handler.Handle(evt, CancellationToken.None));

        // Assert
        exception.Should().BeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate cache after session finalized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsInformationOnSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var evt = new UserLibraryGameAddedEvent(userId, Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — #4559: HybridCache non restituisce un conteggio, il log riporta il tag invalidato
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Dashboard cache invalidated")
                    && v.ToString()!.Contains($"tag user:{userId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log cache invalidation result");
    }

    [Fact]
    public async Task Handle_UsesCorrectUserTag_ForDifferentUsers()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var evt1 = new UserLibraryGameAddedEvent(userId1, Guid.NewGuid());
        var evt2 = new UserChatSavedEvent(userId2, Guid.NewGuid());

        // Act
        await _handler.Handle(evt1, CancellationToken.None);
        await _handler.Handle(evt2, CancellationToken.None);

        // Assert
        _cacheMock.Verify(c => c.RemoveByTagAsync($"user:{userId1}", It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveByTagAsync($"user:{userId2}", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// #4559: dopo un'attività dell'utente, gli insights della dashboard devono essere ricalcolati.
    /// Cache vera (L1 e una L2 in memoria che <see cref="HybridCache"/> usa davvero, vedi <see cref="SharedL2"/>)
    /// e handler della query vero: la voce è scritta con
    /// <see cref="HybridCache"/> di Microsoft, come in produzione. Il controllo prima dell'evento
    /// (voce servita dalla cache) è ciò che rende significativo il passo dopo. Le voci di un altro
    /// utente restano in cache: l'invalidazione è per utente, non globale.
    /// </summary>
    [Fact]
    public async Task Handle_UserActivity_RecomputesInsightsWrittenWithHybridCache()
    {
        var l2 = new SharedL2();
        var (cache, _) = CreateInstance(l2);
        var handler = new UserActivityCacheInvalidationEventHandler(cache, _loggerMock.Object);
        var insights = new CountingInsights();
        var query = Query(insights, cache);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        (await ReadInsightsAsync(query, userId)).Should().Be(CountingInsights.GeneratedAt(1));
        l2.Writes.Should().BePositive("la L2 deve essere davvero in gioco, non scartata da HybridCache");
        (await ReadInsightsAsync(query, otherUserId)).Should().Be(CountingInsights.GeneratedAt(2));
        (await ReadInsightsAsync(query, userId)).Should().Be(CountingInsights.GeneratedAt(1),
            "prima dell'evento la voce è in cache");

        await handler.Handle(new UserLibraryGameAddedEvent(userId, Guid.NewGuid()), CancellationToken.None);

        (await ReadInsightsAsync(query, userId)).Should().Be(CountingInsights.GeneratedAt(3),
            "dopo l'aggiunta di un gioco gli insights devono essere ricalcolati, non serviti dalla cache");
        (await ReadInsightsAsync(query, otherUserId)).Should().Be(CountingInsights.GeneratedAt(2),
            "l'evento di un utente non tocca la cache di un altro");
    }

    /// <summary>
    /// #4559, review — cosa vede un'altra istanza. <see cref="HybridCache.RemoveByTagAsync(string, CancellationToken)"/>
    /// non cancella i dati dalla L2: vi scrive l'istante in cui il tag è stato invalidato. Il wrapper invece
    /// cancellava le chiavi dalla L2. Tre istanze sulla stessa L2: A invalida; B aveva già letto la voce;
    /// C non l'ha mai letta. Oggi l'API gira a istanza singola per contratto (<c>infra/docker-compose.yml</c>,
    /// allarme <c>MultipleApiInstances</c>); per più istanze serve il pub/sub, vedi #4561. Il test documenta
    /// il limite: se diventa rosso, è cambiato il comportamento di <see cref="HybridCache"/>.
    /// </summary>
    [Fact]
    public async Task Handle_UserActivity_AnotherInstanceThatReadTheTag_KeepsServingTheOldL2Entry()
    {
        var l2 = new SharedL2();
        var (cacheA, _) = CreateInstance(l2);
        var (cacheB, l1B) = CreateInstance(l2);
        var (cacheC, _) = CreateInstance(l2);
        var insights = new CountingInsights();
        var handlerOnA = new UserActivityCacheInvalidationEventHandler(cacheA, _loggerMock.Object);
        var userId = Guid.NewGuid();

        (await ReadInsightsAsync(Query(insights, cacheA), userId)).Should().Be(CountingInsights.GeneratedAt(1));
        (await ReadInsightsAsync(Query(insights, cacheB), userId)).Should().Be(CountingInsights.GeneratedAt(1),
            "B trova in L2 la voce scritta da A");

        await handlerOnA.Handle(new UserLibraryGameAddedEvent(userId, Guid.NewGuid()), CancellationToken.None);

        (await ReadInsightsAsync(Query(insights, cacheB), userId)).Should().Be(CountingInsights.GeneratedAt(1),
            "come prima: l'invalidazione su A non tocca la L1 di B");

        l1B.Clear(); // la L1 di B scade: la prossima lettura passa dalla L2
        (await ReadInsightsAsync(Query(insights, cacheB), userId)).Should().Be(CountingInsights.GeneratedAt(1),
            "diverso da prima: la voce vecchia è ancora in L2, e B, che ha già letto il tag, non rilegge "
            + "l'istante d'invalidazione e la accetta");

        (await ReadInsightsAsync(Query(insights, cacheC), userId)).Should().Be(CountingInsights.GeneratedAt(2),
            "C non ha mai letto il tag: rilegge l'istante dalla L2 e ricalcola");
    }

    /// <summary>
    /// #4559: ciò che oggi funziona non si perde. Una voce scritta tramite il wrapper
    /// (<see cref="HybridCacheService"/>, con la chiave e i tag che usa <c>ActivityTimelineService</c>)
    /// deve essere ricalcolata dopo l'evento. Il wrapper qui non ha Redis, quindi nessun set
    /// <c>cache:tag:{tag}</c>: l'invalidazione non dipende più da quel set.
    /// </summary>
    [Fact]
    public async Task Handle_UserActivity_RecomputesEntryWrittenThroughWrapper()
    {
        var (cache, _) = CreateInstance(new SharedL2());
        var handler = new UserActivityCacheInvalidationEventHandler(cache, _loggerMock.Object);
        var userId = Guid.NewGuid();
        var timeline = TimelineThroughWrapper(cache, userId);

        (await timeline.ReadAsync()).Should().Be("calcolo 1");
        (await timeline.ReadAsync()).Should().Be("calcolo 1", "prima dell'evento la voce è in cache");

        await handler.Handle(new UserChatSavedEvent(userId, Guid.NewGuid()), CancellationToken.None);

        (await timeline.ReadAsync()).Should().Be("calcolo 2",
            "dopo il salvataggio di una chat la timeline deve essere ricalcolata");
    }

    /// <summary>
    /// #4559: anche il ramo di <see cref="SessionFinalizedEvent"/>, che invalida il tag globale
    /// <c>activity-timeline</c>, ora passa da <see cref="HybridCache"/> e deve raggiungere le voci
    /// scritte tramite il wrapper.
    /// </summary>
    [Fact]
    public async Task Handle_SessionFinalized_RecomputesTimelineWrittenThroughWrapper()
    {
        var (cache, _) = CreateInstance(new SharedL2());
        var handler = new UserActivityCacheInvalidationEventHandler(cache, _loggerMock.Object);
        var timeline = TimelineThroughWrapper(cache, Guid.NewGuid());

        (await timeline.ReadAsync()).Should().Be("calcolo 1");
        (await timeline.ReadAsync()).Should().Be("calcolo 1", "prima dell'evento la voce è in cache");

        await handler.Handle(
            new SessionFinalizedEvent { SessionId = Guid.NewGuid(), DurationMinutes = 45 },
            CancellationToken.None);

        (await timeline.ReadAsync()).Should().Be("calcolo 2",
            "dopo la chiusura di una sessione la timeline deve essere ricalcolata");
    }

    private static async Task<DateTime> ReadInsightsAsync(GetDashboardInsightsQueryHandler query, Guid userId) =>
        (await query.Handle(new GetDashboardInsightsQuery(userId), CancellationToken.None)).GeneratedAt;

    private static GetDashboardInsightsQueryHandler Query(CountingInsights insights, HybridCache cache) =>
        new(insights.Service, cache, Mock.Of<ILogger<GetDashboardInsightsQueryHandler>>());

    /// <summary>Un'istanza dell'API: la sua L1, e la L2 che le si passa (condivisibile fra istanze).</summary>
    private static (HybridCache Cache, MemoryCache L1) CreateInstance(SharedL2 l2)
    {
        var l1 = new MemoryCache(new MemoryCacheOptions());
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache>(l1);
        services.AddSingleton<IDistributedCache>(l2);
        services.AddHybridCache();
        return (services.BuildServiceProvider().GetRequiredService<HybridCache>(), l1);
    }

    /// <summary>
    /// L2 in memoria, condivisibile fra istanze. È un tipo proprio che delega a
    /// <see cref="MemoryDistributedCache"/>, non <see cref="MemoryDistributedCache"/> nudo:
    /// <c>DefaultHybridCache</c> scarta la L2 quando è esattamente <see cref="MemoryDistributedCache"/>
    /// sopra un <see cref="MemoryCache"/>, e i test resterebbero senza L2.
    /// </summary>
    private sealed class SharedL2 : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));
        private int _writes;

        public int Writes => Volatile.Read(ref _writes);

        public byte[]? Get(string key) => _inner.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => _inner.GetAsync(key, token);

        public void Refresh(string key) => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

        public void Remove(string key) => _inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Interlocked.Increment(ref _writes);
            _inner.Set(key, value, options);
        }

        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Interlocked.Increment(ref _writes);
            return _inner.SetAsync(key, value, options, token);
        }
    }

    /// <summary>
    /// La voce che <c>ActivityTimelineService.GetFilteredActivitiesAsync</c> scrive, con la sua chiave e
    /// i suoi tag, scritta dal wrapper vero sopra la stessa <see cref="HybridCache"/>.
    /// </summary>
    private static CountingWrapperEntry TimelineThroughWrapper(HybridCache cache, Guid userId)
    {
        var wrapper = new HybridCacheService(
            cache,
            Options.Create(new HybridCacheConfiguration { EnableTags = true, MaxTagsPerEntry = 10 }),
            Mock.Of<ILogger<HybridCacheService>>(),
            redis: null);
        return new CountingWrapperEntry(
            wrapper, $"activity-timeline-full:{userId}", ["activity-timeline", $"user:{userId}"]);
    }

    /// <summary>
    /// Un <see cref="IAiInsightsService"/> che conta i calcoli: il calcolo n-esimo porta
    /// <c>GeneratedAt(n)</c>, quindi la risposta dice se è stata ricalcolata o servita dalla cache.
    /// </summary>
    private sealed class CountingInsights
    {
        private static readonly DateTime Origin = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        private int _computations;

        public CountingInsights()
        {
            var mock = new Mock<IAiInsightsService>();
            mock.Setup(s => s.GetInsightsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var n = Interlocked.Increment(ref _computations);
                    return new AiInsightsDto([], GeneratedAt(n), GeneratedAt(n).AddMinutes(15));
                });
            Service = mock.Object;
        }

        public IAiInsightsService Service { get; }

        public static DateTime GeneratedAt(int computation) => Origin.AddMinutes(computation);
    }

    /// <summary>Una voce scritta tramite il wrapper, il cui valore dice quale calcolo l'ha prodotta.</summary>
    private sealed class CountingWrapperEntry(IHybridCacheService wrapper, string key, string[] tags)
    {
        private int _computations;

        public Task<string> ReadAsync() => wrapper.GetOrCreateAsync(
            key,
            _ => Task.FromResult($"calcolo {Interlocked.Increment(ref _computations)}"),
            tags,
            TimeSpan.FromMinutes(5));
    }
}
