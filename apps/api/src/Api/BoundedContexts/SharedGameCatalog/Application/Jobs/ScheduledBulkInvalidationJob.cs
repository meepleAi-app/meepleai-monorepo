using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Observability;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Defensive scheduled bulk-invalidation safety net for the SharedGameCatalog
/// read-model. Issue #613 (P0 cache invalidation resilience).
///
/// <para>
/// Why: the three event-driven invalidation handlers
/// (<c>VectorDocumentIndexedForKbFlagHandler</c>,
/// <c>ToolkitChangedForCatalogAggregatesHandler</c>,
/// <c>AgentDefinitionChangedForCatalogAggregatesHandler</c>) wrap
/// <see cref="HybridCache.RemoveByTagAsync(string, CancellationToken)"/>
/// in a bounded retry policy, but a permanent Redis outage outliving the
/// retry budget can still leave the L2 distributed cache serving stale
/// detail/list payloads (TTL 300s) on multi-replica deployments.
/// </para>
///
/// <para>
/// What: every 15 minutes this job evicts the per-detail tags for the
/// most-trafficked shared games over the last hour plus the global
/// <c>"search-games"</c> list tag. Worst-case staleness is therefore
/// bounded at 15 min instead of the 5-min L2 TTL plus indefinite L1
/// drift, even when event handlers have permanently failed.
/// </para>
///
/// <para>
/// How: query <see cref="GameAnalyticsEventEntity"/> for the top-N
/// SharedGameIds by event volume in the last hour, then loop and invoke
/// <see cref="ICacheInvalidationRetryPolicy"/>. Top-N is bounded
/// (<see cref="DefaultTopN"/>=50) to keep job runtime under a few hundred
/// milliseconds. Background-task pattern: catch all exceptions, never
/// rethrow, surface failure on <see cref="IJobExecutionContext.Result"/>.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
internal sealed class ScheduledBulkInvalidationJob : IJob
{
    internal const int DefaultTopN = 50;

    /// <summary>
    /// Passi di fila che si arrendono prima che il job smetta di provare. Ogni passo costa i suoi tentativi della
    /// retry policy: con la cache che non risponde, tutti i passi della lista farebbero durare il job più del
    /// suo intervallo. Tre di fila dicono che non è un singolo tag a non andare.
    /// </summary>
    internal const int MaxConsecutiveFailures = 3;

    internal static readonly TimeSpan LookbackWindow = TimeSpan.FromHours(1);
    private const string SearchGamesTag = "search-games";
    private const string ListOperationName = "shared-games.list.bulk";
    private const string DetailOperationName = "shared-games.detail.bulk";

    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ICacheInvalidationRetryPolicy _retryPolicy;
    private readonly ILogger<ScheduledBulkInvalidationJob> _logger;

    public ScheduledBulkInvalidationJob(
        MeepleAiDbContext context,
        HybridCache cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        ILogger<ScheduledBulkInvalidationJob> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ct = context.CancellationToken;
        _logger.LogInformation(
            "ScheduledBulkInvalidationJob started: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // 1) Single global list invalidation. Cheap and covers any catalog
            //    list query that could have stale aggregates from the last 15min.
            //
            // #4659: each eviction step is best effort (TryExecuteAsync): this is a safety net for the steps that
            // already gave up elsewhere, so one that gives up here is logged and counted and the rest of the
            // list still runs. After MaxConsecutiveFailures in a row the rest is skipped (SkippedInvalidations):
            // each step costs its retries, and the cache not answering would make the job outlast its interval.
            // Cancellation (Quartz stopping) is not a failure and still propagates.
            var failedInvalidations = 0;
            var consecutiveFailures = 0;
            var listInvalidated = await _retryPolicy.TryExecuteAsync(
                token => _cache.RemoveByTagAsync(SearchGamesTag, token),
                ListOperationName,
                SearchGamesTag,
                _logger,
                ct).ConfigureAwait(false);
            if (!listInvalidated)
            {
                failedInvalidations++;
                consecutiveFailures++;
            }

            // 2) Top-N most-trafficked SharedGames in the last LookbackWindow.
            //    GameAnalyticsEventEntity.GameId is the SharedGameEntity.Id
            //    (see GetCatalogTrendingQueryHandler.ComputeTrendingAsync which
            //    joins against SharedGameEntity on the same column).
            var since = DateTime.UtcNow - LookbackWindow;
            var topGameIds = await _context.Set<GameAnalyticsEventEntity>()
                .AsNoTracking()
                .Where(e => e.Timestamp >= since)
                .GroupBy(e => e.GameId)
                .OrderByDescending(g => g.Count())
                .Take(DefaultTopN)
                .Select(g => g.Key)
                .ToListAsync(ct).ConfigureAwait(false);

            var invalidatedCount = 0;
            var skippedCount = 0;
            foreach (var sharedGameId in topGameIds)
            {
                ct.ThrowIfCancellationRequested();

                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    skippedCount++;
                    continue;
                }

                var detailInvalidated = await _retryPolicy.TryExecuteAsync(
                    token => _cache.RemoveByTagAsync($"shared-game:{sharedGameId}", token),
                    DetailOperationName,
                    $"shared-game:{sharedGameId}",
                    _logger,
                    ct).ConfigureAwait(false);

                if (detailInvalidated)
                {
                    invalidatedCount++;
                    consecutiveFailures = 0;
                }
                else
                {
                    failedInvalidations++;
                    consecutiveFailures++;
                }
            }

            _logger.LogInformation(
                "ScheduledBulkInvalidationJob completed: list_invalidated={ListCount} detail_invalidated={DetailCount} failed={FailedCount} skipped={SkippedCount}",
                listInvalidated ? 1 : 0,
                invalidatedCount,
                failedInvalidations,
                skippedCount);

            if (failedInvalidations > 0)
            {
                MeepleAiMetrics.RecordCacheInvalidationOutcome("shared-games.bulk.job", "failure");
            }

            context.Result = new
            {
                Success = failedInvalidations == 0,
                ListInvalidations = listInvalidated ? 1 : 0,
                DetailInvalidations = invalidatedCount,
                FailedInvalidations = failedInvalidations,
                SkippedInvalidations = skippedCount
            };
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "ScheduledBulkInvalidationJob cancelled");
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // BACKGROUND TASK PATTERN: Quartz job exceptions must not propagate
        // (would terminate the scheduler thread). Surface failure via Result.
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScheduledBulkInvalidationJob failed");
            MeepleAiMetrics.RecordCacheInvalidationOutcome(
                "shared-games.bulk.job",
                "failure");

            context.Result = new
            {
                Success = false,
                Error = ex.Message
            };
        }
#pragma warning restore CA1031
    }
}
