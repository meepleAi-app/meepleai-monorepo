using Api.Infrastructure;
using Api.Observability;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Issue #2248 (epic #2242, Sub #6 Block B): periodic guard against the silent-failure
/// mode that #2243 Block A fixed at the publish-event level.
///
/// The invariant: PdfDocument.ProcessingState == "Ready" ⇒
/// SharedGame.HasKnowledgeBase == true.
///
/// If the tactical mediator.Publish(VectorDocumentIndexedEvent) inside
/// FinalizeProcessingAsync is ever bypassed (regression, new ingestion path
/// added without raising the event, swallowed exception in the projection
/// handler, an event that carried a null SharedGameId, etc.) admin + catalog
/// UIs silently revert to showing "no agent ready" while the catalog is
/// otherwise healthy.
///
/// This job now RECONCILES (not just audits) the drift: every 10 minutes it
/// finds SharedGames that have a Ready PdfDocument but HasKnowledgeBase=false,
/// flips the flag to true, and invalidates the catalog read-model cache the
/// same way <c>VectorDocumentIndexedForKbFlagHandler</c> does — so the
/// "AI-ready" flag becomes visible within one job cycle even when the inline
/// event path failed. Each repaired row still increments
/// <see cref="MeepleAiMetrics.PdfIndexedNoKbFlagTotal"/> and logs at Warning so
/// the SLO=0 signal remains observable (a healthy pipeline reconciles nothing).
///
/// Idempotent and best effort. Bounded to 100 games per run — if drift is
/// widespread the counter climbs across runs and the operator triages from the
/// audit log while the backlog drains.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class KbFlagDriftAuditJob : IJob
{
    private const int MaxGamesPerRun = 100;

    private readonly MeepleAiDbContext _dbContext;
    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _retryPolicy;
    private readonly ILogger<KbFlagDriftAuditJob> _logger;

    public KbFlagDriftAuditJob(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        ILogger<KbFlagDriftAuditJob> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cancellationToken = context.CancellationToken;

        try
        {
            // Distinct SharedGameIds that violate the invariant: a Ready PDF exists
            // but the game's HasKnowledgeBase flag is still false. Projection-only,
            // no tracking. Limited to MaxGamesPerRun per run.
            var driftedGameIds = await _dbContext.PdfDocuments
                .AsNoTracking()
                .Where(p => p.ProcessingState == "Ready" && p.SharedGameId != null)
                .Join(
                    _dbContext.SharedGames.AsNoTracking().Where(g => !g.HasKnowledgeBase),
                    p => p.SharedGameId,
                    g => g.Id,
                    (p, g) => g.Id)
                .Distinct()
                .Take(MaxGamesPerRun)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (driftedGameIds.Count == 0)
            {
                _logger.LogDebug(
                    "KbFlagDriftAuditJob: 0 drifted games (FireTime={FireTime})",
                    context.FireTimeUtc);
                context.Result = new { RepairedGames = 0 };
                return;
            }

            // Load the drifted games TRACKED so we can flip the flag and persist.
            // .AsTracking() is REQUIRED: MeepleAiDbContext defaults to NoTracking
            // (PERF-06), so without it the loaded entities are detached and the
            // SaveChangesAsync below is a silent no-op (the reconcile would never
            // actually repair anything).
            var games = await _dbContext.SharedGames
                .AsTracking()
                .Where(g => driftedGameIds.Contains(g.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var game in games)
            {
                if (game.HasKnowledgeBase)
                {
                    continue; // raced with the inline handler — already healed
                }

                game.HasKnowledgeBase = true;
                MeepleAiMetrics.RecordPdfIndexedNoKbFlagDrift();
                _logger.LogWarning(
                    "KbFlagDrift reconciled: SharedGame {SharedGameId} had a Ready PDF but " +
                    "HasKnowledgeBase=false — flag flipped to true. Root cause: the inline " +
                    "VectorDocumentIndexedEvent projection did not run for this game " +
                    "(#2243 Block A regression, a null SharedGameId on the event, or a new " +
                    "ingestion path bypassing the publish).",
                    game.Id);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Invalidate the catalog read-model cache across replicas so the freshly
            // flipped "AI-ready" flag appears immediately instead of waiting for the
            // L1/L2 TTL — mirrors VectorDocumentIndexedForKbFlagHandler.
            await _retryPolicy.ExecuteAsync(
                token => new ValueTask(_cache.RemoveByTagAcrossReplicasAsync("search-games", token)),
                "kb-reconcile.list",
                cancellationToken).ConfigureAwait(false);

            foreach (var game in games)
            {
                await _retryPolicy.ExecuteAsync(
                    token => new ValueTask(_cache.RemoveByTagAcrossReplicasAsync($"shared-game:{game.Id}", token)),
                    "kb-reconcile.detail",
                    cancellationToken).ConfigureAwait(false);
            }

            _logger.LogError(
                "KbFlagDriftAuditJob: reconciled {Count} drifted games — SLO=0 violated. " +
                "Investigate publish-event chain in UploadPdfCommandHandler.FinalizeProcessingAsync, " +
                "PdfIndexingPipeline and IndexPdfCommandHandler.",
                games.Count);

            context.Result = new { RepairedGames = games.Count };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Background job — must not throw or Quartz suspends the trigger.
        catch (Exception ex)
        {
            _logger.LogError(ex, "KbFlagDriftAuditJob: unexpected error during reconcile");
            context.Result = new { Error = ex.Message };
        }
#pragma warning restore CA1031
    }
}
