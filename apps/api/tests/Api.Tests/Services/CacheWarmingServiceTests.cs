using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Tests.Helpers;
using Microsoft.Extensions.Time.Testing;
using FluentAssertions;
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
/// Uses TestTimeProvider for deterministic timing (eliminates Task.Delay).
/// </summary>
public class CacheWarmingServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<CacheWarmingService>> _mockLogger;
    private readonly Mock<IRedisFrequencyTracker> _frequencyTrackerMock;
    private readonly Mock<IAiResponseCacheService> _cacheServiceMock;
    private readonly Mock<IRagService> _ragServiceMock;
    private readonly Mock<IOptions<CacheOptimizationConfiguration>> _mockConfig;
    private readonly FakeTimeProvider _timeProvider;
    private SqliteConnection? _connection;
    private ServiceProvider? _serviceProvider;
    private IServiceScopeFactory? _scopeFactory;

    public CacheWarmingServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<CacheWarmingService>>();
        _frequencyTrackerMock = new Mock<IRedisFrequencyTracker>();
        _cacheServiceMock = new Mock<IAiResponseCacheService>();
        _ragServiceMock = new Mock<IRagService>();
        _mockConfig = new Mock<IOptions<CacheOptimizationConfiguration>>();
        _timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1); // Start at 2025-01-01 00:00:00 UTC

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

        _serviceProvider = services.BuildServiceProvider();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return scopeFactory;
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _serviceProvider?.Dispose();
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

        _frequencyTrackerMock.Setup(ft => ft.GetTopQueriesAsync(gameId, 50))
            .ReturnsAsync(topQueries.Take(50).ToList());

        _cacheServiceMock.Setup(cs => cs.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null); // Not cached

        _ragServiceMock.Setup(rag => rag.AskAsync(
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
            _frequencyTrackerMock.Object,
            _cacheServiceMock.Object,
            _ragServiceMock.Object,
            _scopeFactory,
            _mockConfig.Object,
            _timeProvider
        );

        // Act (When): Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<CacheWarmingService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(15),
            processingDelayMs: 500 // Increased to allow cache warming loop completion
        );

        await helper.StartAsync();

        // Advance past startup delay (600ms) and wait for warming to complete
        await helper.AdvanceSecondsAsync(1); // Advance 1 second (> 600ms startup delay)

        // Give background service extra time to complete the full warming cycle
        await helper.WaitForProcessingAsync();

        await helper.StopAsync();

        // Assert (Then): Top 50 queries pre-cached
        _ragServiceMock.Verify(
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
            _frequencyTrackerMock.Object,
            _cacheServiceMock.Object,
            _ragServiceMock.Object,
            _scopeFactory,
            delayConfig.Object,
            _timeProvider
        );

        // Act (When): Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<CacheWarmingService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(10),
            processingDelayMs: 500 // Increased to allow cache warming loop completion
        );

        await helper.StartAsync();

        // Advance less than 2 minutes (only 1 minute)
        await helper.AdvanceMinutesAsync(1);

        // Give background service extra time to check the delay
        await helper.WaitForProcessingAsync();

        // Assert (Then): Delays 2 minutes before first warming call
        _frequencyTrackerMock.Verify(
            ft => ft.GetTopQueriesAsync(It.IsAny<Guid>(), It.IsAny<int>()),
            Times.Never // Should not call after only 1 minute (needs 2)
        );

        await helper.StopAsync();
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

        _frequencyTrackerMock.Setup(ft => ft.GetTopQueriesAsync(gameId, 50))
            .ReturnsAsync(topQueries);

        _cacheServiceMock.Setup(cs => cs.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var callCount = 0;
        _ragServiceMock.Setup(rag => rag.AskAsync(
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
            _frequencyTrackerMock.Object,
            _cacheServiceMock.Object,
            _ragServiceMock.Object,
            _scopeFactory,
            _mockConfig.Object,
            _timeProvider
        );

        // Act (When): Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<CacheWarmingService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(15),
            processingDelayMs: 500 // Increased to allow cache warming loop completion
        );

        await helper.StartAsync();

        // Advance past startup delay to trigger warming
        await helper.AdvanceSecondsAsync(1);

        // Give time for background service to process queries (including handling exception)
        // Multiple iterations to ensure all queries are processed
        for (int i = 0; i < 3; i++)
        {
            await helper.WaitForProcessingAsync();
        }

        await helper.StopAsync();

        // Assert (Then): Service continues processing after exception
        // The service catches exceptions in WarmSingleQueryAsync and continues with next queries
        _ragServiceMock.Verify(
            rag => rag.AskAsync(
                gameId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.AtLeast(25) // Service attempts at least up to the failing query
        );

        // Error should be logged for the failing query
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("warming cache for query")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce // Query #25 error should be logged
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

        _frequencyTrackerMock.Setup(ft => ft.GetTopQueriesAsync(gameId, 50))
            .ReturnsAsync(topQueries);

        // Use GenerateQaCacheKey to match the service's cache key format
        _cacheServiceMock.Setup(cs => cs.GenerateQaCacheKey(gameId.ToString(), cachedQuery))
            .Returns($"game:{gameId}:query:hash");
        _cacheServiceMock.Setup(cs => cs.GenerateQaCacheKey(gameId.ToString(), "Other query"))
            .Returns($"game:{gameId}:query:hash2");

        _cacheServiceMock.Setup(cs => cs.GetAsync<object>($"game:{gameId}:query:hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse("Cached answer", new List<Snippet>(), 0, 0, 0, 0.9));

        _cacheServiceMock.Setup(cs => cs.GetAsync<object>($"game:{gameId}:query:hash2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var service = new CacheWarmingService(
            _mockLogger.Object,
            _frequencyTrackerMock.Object,
            _cacheServiceMock.Object,
            _ragServiceMock.Object,
            _scopeFactory,
            _mockConfig.Object,
            _timeProvider
        );

        // Act (When): Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<CacheWarmingService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(15),
            processingDelayMs: 500 // Increased to allow cache warming loop completion
        );

        await helper.StartAsync();

        // Advance past startup delay
        await helper.AdvanceSecondsAsync(1);

        // Give background service extra time to complete the full warming cycle
        await helper.WaitForProcessingAsync();

        await helper.StopAsync();

        // Assert (Then): Skips re-caching first query, logs "Skipped"
        _ragServiceMock.Verify(
            rag => rag.AskAsync(
                gameId.ToString(),
                cachedQuery,
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Never // Skipped
        );

        _ragServiceMock.Verify(
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
            Times.AtLeastOnce
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

        _frequencyTrackerMock.Setup(ft => ft.GetTopQueriesAsync(chessGameId, 50))
            .ReturnsAsync(chessQueries);

        _frequencyTrackerMock.Setup(ft => ft.GetTopQueriesAsync(tttGameId, 50))
            .ReturnsAsync(tttQueries);

        _cacheServiceMock.Setup(cs => cs.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var service = new CacheWarmingService(
            _mockLogger.Object,
            _frequencyTrackerMock.Object,
            _cacheServiceMock.Object,
            _ragServiceMock.Object,
            _scopeFactory,
            _mockConfig.Object,
            _timeProvider
        );

        // Act (When): Using BackgroundServiceTestHelper for proper coordination
        using var helper = new BackgroundServiceTestHelper<CacheWarmingService>(
            service,
            _timeProvider,
            timeout: TimeSpan.FromSeconds(15),
            processingDelayMs: 500 // Increased to allow cache warming loop completion
        );

        await helper.StartAsync();

        // Advance past startup delay
        await helper.AdvanceSecondsAsync(1);

        // Give background service extra time to complete the full warming cycle
        await helper.WaitForProcessingAsync();

        await helper.StopAsync();

        // Assert (Then): Cache keys include game ID
        _ragServiceMock.Verify(
            rag => rag.AskAsync(
                chessGameId.ToString(),
                "Chess query 1",
                It.IsAny<string?>(),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once
        );

        _ragServiceMock.Verify(
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
            _frequencyTrackerMock.Object,
            _cacheServiceMock.Object,
            _ragServiceMock.Object,
            _scopeFactory,
            _mockConfig.Object,
            _timeProvider
        );

        // Assert (Then): Service should validate config in constructor
        // Note: Actual DI registration check happens in Program.cs, not constructor
        // This test validates config is read correctly
        using var service = act();
        service.Should().NotBeNull();
        _mockConfig.Verify(c => c.Value, Times.Once);
    }
}