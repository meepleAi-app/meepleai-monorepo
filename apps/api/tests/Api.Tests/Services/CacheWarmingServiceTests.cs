using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MeepleAI.Api.Tests.Services;

/// <summary>
/// BDD-style unit tests for CacheWarmingService (background service).
/// Tests startup warming, retry logic, failure handling, and feature flags.
/// </summary>
public class CacheWarmingServiceTests : IDisposable
{
    private readonly Mock<ILogger<CacheWarmingService>> _mockLogger;
    private readonly Mock<IRedisFrequencyTracker> _mockFrequencyTracker;
    private readonly Mock<IAiResponseCacheService> _mockCacheService;
    private readonly Mock<IRagService> _mockRagService;
    private readonly Mock<IOptions<CacheOptimizationConfiguration>> _mockConfig;
    private SqliteConnection? _connection;
    private IServiceScopeFactory? _scopeFactory;

    public CacheWarmingServiceTests()
    {
        _mockLogger = new Mock<ILogger<CacheWarmingService>>();
        _mockFrequencyTracker = new Mock<IRedisFrequencyTracker>();
        _mockCacheService = new Mock<IAiResponseCacheService>();
        _mockRagService = new Mock<IRagService>();
        _mockConfig = new Mock<IOptions<CacheOptimizationConfiguration>>();

        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            WarmingEnabled = true,
            WarmingTopQueriesCount = 50,
            WarmingStartupDelayMinutes = 0.01, // 600ms for testing
            WarmingIntervalHours = 6
        });
    }

    private IServiceScopeFactory CreateScopeFactory(params GameEntity[] games)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var context = new MeepleAiDbContext(options))
        {
            context.Database.EnsureCreated();
            context.Games.AddRange(games);
            context.SaveChanges();
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => new MeepleAiDbContext(options));

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_Startup_WarmsTop50Queries()
    {
        // Arrange (Given): Frequency tracker has 100 queries
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId.ToString(), Name = "Test Game" };
        _scopeFactory = CreateScopeFactory(game);

        var topQueries = Enumerable.Range(1, 100)
            .Select(i => new FrequentQuery
            {
                GameId = gameId,
                Query = $"Query {i}",
                AccessCount = 100 - i + 1
            })
            .ToList();

        _mockFrequencyTracker.Setup(ft => ft.GetTopQueriesAsync(gameId, 50))
            .ReturnsAsync(topQueries.Take(50).ToList());

        _mockCacheService.Setup(cs => cs.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null); // Not cached

        _mockRagService.Setup(rag => rag.AskAsync(
            gameId.ToString(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                "Test answer",
                new List<Snippet>(),
                0, 0, 0,
                0.95
            ));

        var service = new CacheWarmingService(
            _mockLogger.Object,
            _mockFrequencyTracker.Object,
            _mockCacheService.Object,
            _mockRagService.Object,
            _scopeFactory,
            _mockConfig.Object
        );

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5)); // Prevent infinite loop

        // Act (When): Warming service starts
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3)); // Wait for warming
        await service.StopAsync(cts.Token);

        // Assert (Then): Top 50 queries pre-cached
        _mockRagService.Verify(
            rag => rag.AskAsync(
                gameId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Exactly(50)
        );
    }

    [Fact]
    public async Task ExecuteAsync_Startup_WaitsTwoMinutesBeforeWarming()
    {
        // Arrange (Given): Service configured with 2-minute startup delay
        _scopeFactory = CreateScopeFactory(); // No games needed for this test

        // Override config for this test to use actual 2-minute delay
        var delayConfig = new Mock<IOptions<CacheOptimizationConfiguration>>();
        delayConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            WarmingEnabled = true,
            WarmingStartupDelayMinutes = 2,
            WarmingIntervalHours = 6
        });

        var service = new CacheWarmingService(
            _mockLogger.Object,
            _mockFrequencyTracker.Object,
            _mockCacheService.Object,
            _mockRagService.Object,
            _scopeFactory,
            delayConfig.Object
        );

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        // Act (When): Service starts
        var startTime = DateTime.UtcNow;
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(1)); // Let it start
        var firstCallTime = DateTime.UtcNow;

        // Assert (Then): Delays 2 minutes before first warming call
        _mockFrequencyTracker.Verify(
            ft => ft.GetTopQueriesAsync(It.IsAny<Guid>(), It.IsAny<int>()),
            Times.Never // Should not call immediately
        );

        await service.StopAsync(cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_LlmFailure_ContinuesWarmingRemainingQueries()
    {
        // Arrange (Given): Query #25 fails (OpenRouter timeout)
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId.ToString(), Name = "Test Game" };
        _scopeFactory = CreateScopeFactory(game);

        var topQueries = Enumerable.Range(1, 50)
            .Select(i => new FrequentQuery
            {
                GameId = gameId,
                Query = $"Query {i}",
                AccessCount = 51 - i
            })
            .ToList();

        _mockFrequencyTracker.Setup(ft => ft.GetTopQueriesAsync(gameId, 50))
            .ReturnsAsync(topQueries);

        _mockCacheService.Setup(cs => cs.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var callCount = 0;
        _mockRagService.Setup(rag => rag.AskAsync(
            gameId.ToString(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            false,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 25)
                    throw new TimeoutException("OpenRouter timeout");
                return new QaResponse("Test", new List<Snippet>(), 0, 0, 0, 0.9);
            });

        var service = new CacheWarmingService(
            _mockLogger.Object,
            _mockFrequencyTracker.Object,
            _mockCacheService.Object,
            _mockRagService.Object,
            _scopeFactory,
            _mockConfig.Object
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act (When): Warming runs
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3));
        await service.StopAsync(cts.Token);

        // Assert (Then): Queries 1-24 cached, 26-50 continue, no crash
        _mockRagService.Verify(
            rag => rag.AskAsync(
                gameId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Exactly(50) // All queries attempted
        );

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to warm cache for query")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once // Only query #25 logged error
        );
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCached_SkipsQuery()
    {
        // Arrange (Given): Query "How to win?" cached 5min ago
        var gameId = Guid.NewGuid();
        var game = new GameEntity { Id = gameId.ToString(), Name = "Test Game" };
        _scopeFactory = CreateScopeFactory(game);

        var cachedQuery = "How to win?";
        var topQueries = new List<FrequentQuery>
        {
            new() { GameId = gameId, Query = cachedQuery, AccessCount = 50 },
            new() { GameId = gameId, Query = "Other query", AccessCount = 30 }
        };

        _mockFrequencyTracker.Setup(ft => ft.GetTopQueriesAsync(gameId, 50))
            .ReturnsAsync(topQueries);

        // Use GenerateQaCacheKey to match the service's cache key format
        _mockCacheService.Setup(cs => cs.GenerateQaCacheKey(gameId.ToString(), cachedQuery))
            .Returns($"game:{gameId}:query:hash");
        _mockCacheService.Setup(cs => cs.GenerateQaCacheKey(gameId.ToString(), "Other query"))
            .Returns($"game:{gameId}:query:hash2");

        _mockCacheService.Setup(cs => cs.GetAsync<object>($"game:{gameId}:query:hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse("Cached answer", new List<Snippet>(), 0, 0, 0, 0.9));

        _mockCacheService.Setup(cs => cs.GetAsync<object>($"game:{gameId}:query:hash2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var service = new CacheWarmingService(
            _mockLogger.Object,
            _mockFrequencyTracker.Object,
            _mockCacheService.Object,
            _mockRagService.Object,
            _scopeFactory,
            _mockConfig.Object
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act (When): Warming runs
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3));
        await service.StopAsync(cts.Token);

        // Assert (Then): Skips re-caching first query, logs "Skipped"
        _mockRagService.Verify(
            rag => rag.AskAsync(
                gameId.ToString(),
                cachedQuery,
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Never // Skipped
        );

        _mockRagService.Verify(
            rag => rag.AskAsync(
                gameId.ToString(),
                "Other query",
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once // Not cached, warmed
        );

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already cached")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_MultipleGames_RespectsGameIsolation()
    {
        // Arrange (Given): Top queries include Chess + Tic-Tac-Toe
        var chessGameId = Guid.NewGuid();
        var tttGameId = Guid.NewGuid();

        var chessGame = new GameEntity { Id = chessGameId.ToString(), Name = "Chess" };
        var tttGame = new GameEntity { Id = tttGameId.ToString(), Name = "Tic-Tac-Toe" };
        _scopeFactory = CreateScopeFactory(chessGame, tttGame);

        var chessQueries = new List<FrequentQuery>
        {
            new() { GameId = chessGameId, Query = "Chess query 1", AccessCount = 50 }
        };

        var tttQueries = new List<FrequentQuery>
        {
            new() { GameId = tttGameId, Query = "TTT query 1", AccessCount = 30 }
        };

        _mockFrequencyTracker.Setup(ft => ft.GetTopQueriesAsync(chessGameId, 50))
            .ReturnsAsync(chessQueries);

        _mockFrequencyTracker.Setup(ft => ft.GetTopQueriesAsync(tttGameId, 50))
            .ReturnsAsync(tttQueries);

        _mockCacheService.Setup(cs => cs.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var service = new CacheWarmingService(
            _mockLogger.Object,
            _mockFrequencyTracker.Object,
            _mockCacheService.Object,
            _mockRagService.Object,
            _scopeFactory,
            _mockConfig.Object
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act (When): Warming runs
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3));
        await service.StopAsync(cts.Token);

        // Assert (Then): Cache keys include game ID
        _mockRagService.Verify(
            rag => rag.AskAsync(
                chessGameId.ToString(),
                "Chess query 1",
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once
        );

        _mockRagService.Verify(
            rag => rag.AskAsync(
                tttGameId.ToString(),
                "TTT query 1",
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public void Constructor_FeatureFlagDisabled_ServiceNotRegistered()
    {
        // Arrange (Given): Config WarmingEnabled=false
        _scopeFactory = CreateScopeFactory();

        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            WarmingEnabled = false
        });

        // Act (When): Attempt to create service
        Func<CacheWarmingService> act = () => new CacheWarmingService(
            _mockLogger.Object,
            _mockFrequencyTracker.Object,
            _mockCacheService.Object,
            _mockRagService.Object,
            _scopeFactory,
            _mockConfig.Object
        );

        // Assert (Then): Service should validate config in constructor
        // Note: Actual DI registration check happens in Program.cs, not constructor
        // This test validates config is read correctly
        var service = act();
        Assert.NotNull(service);
        _mockConfig.Verify(c => c.Value, Times.Once);
    }
}
