using System.Collections.Concurrent;
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.SharedKernel.Application;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// End-to-end integration tests for the player-statistics cache (#2438, PR-B):
/// proves both that <see cref="GetPlayerStatisticsQuery"/> is cached AND that
/// <c>PlayRecordStatsCacheInvalidationHandler</c> evicts the per-user tag when a record
/// completes (raising <see cref="Api.BoundedContexts.GameManagement.Domain.Events.PlayRecordCompletedEvent"/>).
///
/// Uses a stateful in-process <see cref="IHybridCacheService"/> double (<see cref="StatefulHybridCache"/>)
/// that caches by key and evicts by tag — neither the default Moq mock nor
/// <c>PassthroughHybridCache</c> can prove invalidation (one never caches, the other never evicts).
///
/// Forces <see cref="DomainEventDispatchMode.Hybrid"/> so the completion event fires inline through
/// MediatR after SaveChangesAsync (the default OutboxOnly queues for a background processor that is
/// not registered in this minimal test DI — see FinalizeSessionSingleDispatchIntegrationTests).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2438")]
public sealed class PlayerStatisticsCacheTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private readonly TestTimeProvider _timeProvider = new();
    private readonly StatefulHybridCache _cache = new();

    public PlayerStatisticsCacheTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized.");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_playstats_cache_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        services.AddScoped<IPlayRecordRepository, PlayRecordRepository>();
        services.AddScoped<PlayRecordPermissionChecker>();
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<IGameCoreDataProvider, GameCoreDataProvider>();
        services.AddSingleton<TimeProvider>(_timeProvider);

        // Stateful cache double (last registration wins) so the read path actually caches and the
        // invalidation handler's RemoveByTagAsync actually evicts — required to prove invalidation.
        services.AddSingleton<IHybridCacheService>(_cache);

        // Force inline MediatR dispatch so PlayRecordCompletedEvent reaches the invalidation handler.
        services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
            Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }

    [Fact]
    public async Task Stats_AreServedFromCache_OnRepeatQueryWithoutChange()
    {
        // Arrange — one completed record for the user.
        var userId = await SeedTestUserAsync();
        await CreateCompletedRecordAsync(userId);

        // Act — first query computes + caches; second query (no change) should hit the cache.
        var first = await SendInScopeAsync(new GetPlayerStatisticsQuery(userId, null, null));
        var computesAfterFirst = _cache.FactoryInvocations;
        var second = await SendInScopeAsync(new GetPlayerStatisticsQuery(userId, null, null));

        // Assert — same value, and the factory ran only once across both reads (cache hit on 2nd).
        first.TotalSessions.Should().Be(1);
        second.TotalSessions.Should().Be(1);
        _cache.FactoryInvocations.Should().Be(computesAfterFirst,
            "the second query with no intervening change should be served from cache");
    }

    [Fact]
    public async Task Stats_AreInvalidated_WhenANewRecordCompletes()
    {
        // Arrange — one completed record, query once to populate the cache.
        var userId = await SeedTestUserAsync();
        await CreateCompletedRecordAsync(userId);
        var first = await SendInScopeAsync(new GetPlayerStatisticsQuery(userId, null, null));
        first.TotalSessions.Should().Be(1);

        // Act — completing a second record raises PlayRecordCompletedEvent → invalidation handler
        // evicts player-stats:{userId}; the next query must recompute (NOT serve the stale "1").
        await CreateCompletedRecordAsync(userId);
        var second = await SendInScopeAsync(new GetPlayerStatisticsQuery(userId, null, null));

        // Assert — fresh value reflects both records → cache was evicted, not stale.
        second.TotalSessions.Should().Be(2);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<TResult> SendInScopeAsync<TResult>(IRequest<TResult> request)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(request, TestCancellationToken);
    }

    private async Task SendInScopeAsync(IRequest request)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(request, TestCancellationToken);
    }

    private async Task<(Guid Id, string Title)> CreateTestGameAsync()
    {
        var id = Guid.NewGuid();
        var title = $"Test Game {id:N}";

        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);

        return (id, title);
    }

    private async Task<Guid> SeedTestUserAsync()
    {
        var id = Guid.NewGuid();
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<UserEntity>().Add(new UserEntity
        {
            Id = id,
            Email = $"stats-cache-{id:N}@meepleai.dev",
            DisplayName = "Stats Cache User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestCancellationToken);
        return id;
    }

    /// <summary>
    /// Creates a play record owned by <paramref name="userId"/> and completes it, raising
    /// PlayRecordCompletedEvent. Build from the canonical Create + Complete command flow.
    /// </summary>
    private async Task CreateCompletedRecordAsync(Guid userId)
    {
        var (gameId, gameTitle) = await CreateTestGameAsync();

        var recordId = await SendInScopeAsync(new CreatePlayRecordCommand(
            userId,
            gameId,
            gameTitle,
            _timeProvider.UtcNow.AddHours(-1),
            PlayRecordVisibility.Private));

        await SendInScopeAsync(new CompletePlayRecordCommand(recordId, userId, TimeSpan.FromHours(1)));
    }

    /// <summary>
    /// In-process <see cref="IHybridCacheService"/> double that actually caches by key and evicts by
    /// tag, so an end-to-end test can distinguish a cache hit (stale) from an eviction (recompute).
    /// </summary>
    private sealed class StatefulHybridCache : IHybridCacheService
    {
        private readonly ConcurrentDictionary<string, object> _store = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, HashSet<string>> _tagToKeys = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        /// <summary>Count of factory executions (cache misses) — lets a test assert hit vs miss.</summary>
        public int FactoryInvocations { get; private set; }

        public async Task<T> GetOrCreateAsync<T>(
            string cacheKey,
            Func<CancellationToken, Task<T>> factory,
            string[]? tags = null,
            TimeSpan? expiration = null,
            CancellationToken ct = default) where T : class
        {
            if (_store.TryGetValue(cacheKey, out var cached))
            {
                return (T)cached;
            }

            var value = await factory(ct).ConfigureAwait(false);

            lock (_gate)
            {
                FactoryInvocations++;
                _store[cacheKey] = value;
                foreach (var tag in tags ?? Array.Empty<string>())
                {
                    var keys = _tagToKeys.GetOrAdd(tag, _ => new HashSet<string>(StringComparer.Ordinal));
                    keys.Add(cacheKey);
                }
            }

            return value;
        }

        public Task RemoveAsync(string cacheKey, CancellationToken ct = default)
        {
            _store.TryRemove(cacheKey, out _);
            return Task.CompletedTask;
        }

        public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default)
        {
            var removed = 0;
            lock (_gate)
            {
                if (_tagToKeys.TryRemove(tag, out var keys))
                {
                    foreach (var key in keys)
                    {
                        if (_store.TryRemove(key, out _))
                        {
                            removed++;
                        }
                    }
                }
            }

            return Task.FromResult(removed);
        }

        public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default)
            => Task.FromResult(0);

        public Task<int> RemoveByTagAcrossReplicasAsync(string tag, CancellationToken ct = default)
            => RemoveByTagAsync(tag, ct);

        public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default)
            => Task.FromResult(new HybridCacheStats());
    }
}
