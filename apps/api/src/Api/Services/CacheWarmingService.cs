using System.Net.Http;
using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Background service for proactive cache warming of frequently accessed queries.
/// AI-10: Cache Optimization - Reduces latency by pre-caching hot queries during off-peak periods.
/// Runs periodically (configurable interval) with startup delay to avoid interfering with application startup.
/// </summary>
public class CacheWarmingService : BackgroundService
{
    private readonly ILogger<CacheWarmingService> _logger;
    private readonly IRedisFrequencyTracker _frequencyTracker;
    private readonly IAiResponseCacheService _cacheService;
    private readonly IRagService _ragService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CacheOptimizationConfiguration _config;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the CacheWarmingService.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="frequencyTracker">Frequency tracker for identifying hot queries</param>
    /// <param name="cacheService">Cache service for checking if queries are already cached</param>
    /// <param name="ragService">RAG service for executing queries to populate cache</param>
    /// <param name="scopeFactory">Service scope factory for creating scoped DbContext</param>
    /// <param name="config">Cache optimization configuration</param>
    /// <param name="timeProvider">Time provider for testable timing operations (null defaults to System)</param>
    public CacheWarmingService(
        ILogger<CacheWarmingService> logger,
        IRedisFrequencyTracker frequencyTracker,
        IAiResponseCacheService cacheService,
        IRagService ragService,
        IServiceScopeFactory scopeFactory,
        IOptions<CacheOptimizationConfiguration> config,
        TimeProvider? timeProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _frequencyTracker = frequencyTracker ?? throw new ArgumentNullException(nameof(frequencyTracker));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Feature flag: Exit early if warming is disabled
        if (!_config.WarmingEnabled)
        {
            _logger.LogInformation("Cache warming is disabled in configuration. Service will not run.");
            return;
        }

        _logger.LogInformation(
            "Cache warming service starting. Startup delay: {DelayMinutes} minutes, Interval: {IntervalHours} hours",
            _config.WarmingStartupDelayMinutes, _config.WarmingIntervalHours);

        try
        {
            // Startup delay: Wait before first warming cycle to avoid interfering with application startup
            await Task.Delay(
                TimeSpan.FromMinutes(_config.WarmingStartupDelayMinutes),
                _timeProvider,
                stoppingToken);

            // Periodic warming loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await WarmCacheAsync(stoppingToken);

                    _logger.LogInformation(
                        "Cache warming cycle completed. Next cycle in {IntervalHours} hours.",
                        _config.WarmingIntervalHours);

                    // Wait for next warming interval
                    await Task.Delay(
                        TimeSpan.FromHours(_config.WarmingIntervalHours),
                        _timeProvider,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    _logger.LogInformation("Cache warming service is shutting down (cancellation requested).");
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError(ex, "Invalid operation during cache warming cycle. Will retry after interval.");
                    await Task.Delay(TimeSpan.FromHours(_config.WarmingIntervalHours), _timeProvider, stoppingToken);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "HTTP error during cache warming cycle. Will retry after interval.");
                    await Task.Delay(TimeSpan.FromHours(_config.WarmingIntervalHours), _timeProvider, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown
            _logger.LogInformation("Cache warming service stopped.");
        }
    }

    /// <summary>
    /// Performs a single cache warming cycle.
    /// Gets top N queries and pre-caches them if not already cached.
    /// The queries themselves contain their game IDs, so we process them directly.
    /// </summary>
    private async Task WarmCacheAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting cache warming cycle. Top {Count} queries will be pre-cached.",
            _config.WarmingTopQueriesCount);

        try
        {
            // Note: For simplicity, tests will mock the frequency tracker to return queries
            // for specific games. In production, we would iterate through all games from database.
            // Since FrequentQuery contains GameId, we can process them directly.

            // Get top queries (tests will set up multiple games via mock)
            // In tests, the mock returns empty for most game IDs, but specific queries for test games
            var allQueries = new List<FrequentQuery>();

            // Get all active game IDs from database
            var gameIds = await GetAllGameIdsAsync(cancellationToken);

            foreach (var gameId in gameIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var queries = await _frequencyTracker.GetTopQueriesAsync(
                    gameId,
                    _config.WarmingTopQueriesCount);

                allQueries.AddRange(queries);
            }

            _logger.LogDebug("Found {Count} queries to warm across all games", allQueries.Count);

            foreach (var query in allQueries)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await WarmSingleQueryAsync(query, cancellationToken);
            }

            _logger.LogInformation("Cache warming cycle completed successfully.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during cache warming cycle");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during cache warming cycle");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogInformation(ex, "Cache warming cycle cancelled");
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Cache warming cycle operation cancelled");
            throw;
        }
    }

    /// <summary>
    /// Warms cache for a single query if not already cached.
    /// </summary>
    private async Task WarmSingleQueryAsync(FrequentQuery frequentQuery, CancellationToken cancellationToken)
    {
        try
        {
            // Build cache key using AiResponseCacheService's key generation method
            var cacheKey = _cacheService.GenerateQaCacheKey(
                frequentQuery.GameId.ToString(),
                frequentQuery.Query);

            // Check if already cached
            var cachedValue = await _cacheService.GetAsync<object>(cacheKey, cancellationToken);
            if (cachedValue != null)
            {
                _logger.LogDebug(
                    "Query already cached, skipping: {Query} (game {GameId})",
                    frequentQuery.Query, frequentQuery.GameId);
                return;
            }

            // Execute query to populate cache
            _logger.LogDebug(
                "Warming cache for query: {Query} (game {GameId}, access count: {AccessCount})",
                frequentQuery.Query, frequentQuery.GameId, frequentQuery.AccessCount);

            // Call RAG service to execute query and cache the result (bypassCache: false)
            await _ragService.AskAsync(
                frequentQuery.GameId.ToString(),
                frequentQuery.Query,
                language: null,
                bypassCache: false,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Successfully warmed cache for query: {Query} (game {GameId})",
                frequentQuery.Query, frequentQuery.GameId);
        }
        catch (InvalidOperationException ex)
        {
            // Background service resilience: individual failures should not crash warming
            _logger.LogError(ex, "Invalid operation warming cache for query: {Query} (game {GameId})",
                frequentQuery.Query, frequentQuery.GameId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error warming cache for query: {Query} (game {GameId})",
                frequentQuery.Query, frequentQuery.GameId);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Task cancelled warming cache for query: {Query} (game {GameId})",
                frequentQuery.Query, frequentQuery.GameId);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Operation cancelled warming cache for query: {Query} (game {GameId})",
                frequentQuery.Query, frequentQuery.GameId);
        }
    }

    /// <summary>
    /// Gets list of game IDs to check for frequent queries.
    /// Queries the database for all active games.
    /// </summary>
    private async Task<List<Guid>> GetAllGameIdsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameIds = await dbContext.Games
            .Select(g => Guid.Parse(g.Id))
            .ToListAsync(cancellationToken);

        return gameIds;
    }
}
