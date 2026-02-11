using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Daily background job that pre-computes trending game scores and warms the cache.
/// Invalidates existing cache and triggers fresh computation.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
[DisallowConcurrentExecution]
internal sealed class CalculateTrendingJob : IJob
{
    private const string CacheKey = "catalog:trending";

    private readonly IMediator _mediator;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<CalculateTrendingJob> _logger;

    public CalculateTrendingJob(
        IMediator mediator,
        IHybridCacheService cache,
        ILogger<CalculateTrendingJob> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "CalculateTrendingJob started: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // Invalidate existing cache to force fresh computation
            await _cache.RemoveAsync(CacheKey, context.CancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Invalidated trending cache, triggering fresh computation");

            // Trigger fresh computation via the query handler (which will cache the result)
            var trending = await _mediator.Send(
                new GetCatalogTrendingQuery(Limit: 10),
                context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "CalculateTrendingJob completed: {GameCount} trending games computed, TopScore={TopScore}",
                trending.Count,
                trending.Count > 0 ? trending[0].Score : 0);

            context.Result = new
            {
                Success = true,
                GamesComputed = trending.Count,
                TopScore = trending.Count > 0 ? trending[0].Score : 0
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "CalculateTrendingJob failed");

            context.Result = new
            {
                Success = false,
                Error = ex.Message
            };

            // Don't rethrow - Quartz will mark job as failed
        }
#pragma warning restore CA1031
    }
}
