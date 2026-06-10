using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Issue #1823 Wave 3 M9: Quartz singleton scheduler that drives the Wikidata
/// cover enrichment pipeline. Runs every 1 minute via Quartz; per tick:
/// <list type="number">
///   <item>Queries up to <see cref="BatchSize"/> <c>SharedGame</c> IDs ready for
///   enrichment (never-attempted OR retry-eligible per
///   <see cref="WikidataCoverEnrichmentAttempt.NextRetryAt"/>).</item>
///   <item>Dispatches <see cref="EnrichCatalogCoverCommand"/> per game via MediatR.</item>
///   <item>Records a <see cref="WikidataCoverEnrichmentAttempt"/> classified by
///   <see cref="IWikidataCoverEnrichmentRetryPolicy"/> (DEC-3j semantics).</item>
///   <item>Throttles 1s between games to respect the shared Wikimedia 5 RPS cap
///   (DEC-3e) alongside the in-process token bucket.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>Per ADR DEC-3e: single-pod batch (HPA=1) — no distributed lock needed.
/// The <c>[DisallowConcurrentExecution]</c> attribute belt-and-braces enforces
/// the singleton at Quartz level.</para>
///
/// <para>Pattern mirror of <c>CatalogSeedFetchJob</c> (#1903 M5): service-provider
/// scoping per execution, internal <see cref="RunBatchAsync"/> hook for unit
/// tests that drive the batch without spinning up Quartz. Per-item exceptions
/// are caught and logged so a single bad game does not crash the whole batch —
/// the affected attempt simply never gets recorded and the game stays in the
/// "never attempted" set for the next tick.</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class WikidataCoverEnrichmentJob : IJob
{
    /// <summary>
    /// Maximum number of games processed per Quartz tick. With the 1s
    /// inter-item throttle that caps throughput at ~30/min/pod, in line with
    /// ADR DEC-3e's 5 RPS budget across the SPARQL + Commons hops per game.
    /// </summary>
    public const int BatchSize = 30;

    /// <summary>Inter-item throttle (ms). Respects the shared 5 RPS Wikimedia cap.</summary>
    public const int DelayBetweenItemsMs = 1000;

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WikidataCoverEnrichmentJob> _logger;

    public WikidataCoverEnrichmentJob(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<WikidataCoverEnrichmentJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("WikidataCoverEnrichmentJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();

        var attempts = scope.ServiceProvider.GetRequiredService<IWikidataCoverEnrichmentAttemptRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var policy = scope.ServiceProvider.GetRequiredService<IWikidataCoverEnrichmentRetryPolicy>();

        await RunBatchAsync(attempts, uow, mediator, policy, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal batch runner — extracted from <see cref="Execute"/> so unit tests
    /// can drive it directly without spinning up the Quartz scheduler.
    /// </summary>
    internal async Task RunBatchAsync(
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IUnitOfWork uow,
        IMediator mediator,
        IWikidataCoverEnrichmentRetryPolicy policy,
        CancellationToken ct)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var dueGameIds = await attempts
            .GetGameIdsDueForEnrichmentAsync(BatchSize, nowUtc, ct)
            .ConfigureAwait(false);

        if (dueGameIds.Count == 0)
        {
            _logger.LogDebug("WikidataCoverEnrichmentJob: no games due for enrichment this tick.");
            return;
        }

        _logger.LogInformation(
            "WikidataCoverEnrichmentJob: processing {Count} games this tick.",
            dueGameIds.Count);

        var processed = 0;
        foreach (var gameId in dueGameIds)
        {
            if (ct.IsCancellationRequested)
            {
                _logger.LogInformation("WikidataCoverEnrichmentJob: cancellation requested after {Processed} games.", processed);
                break;
            }

            try
            {
                await ProcessGameAsync(attempts, uow, mediator, policy, gameId, ct).ConfigureAwait(false);
                processed++;
            }
            // OperationCanceledException is intentionally allowed to propagate
            // (filter excludes it); any other exception is logged and the batch
            // continues with the next game.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "WikidataCoverEnrichmentJob: unhandled exception while processing game {GameId}; skipping to next.",
                    gameId);
            }

            // Throttle. Skipped between game N and game N+1, NOT after the last
            // game (the job's 1-minute trigger interval is already a natural
            // break). Also skip the delay when the CT is already signalled so
            // shutdown drains cleanly — the loop-top check on the next iteration
            // catches the cancellation and breaks gracefully without throwing.
            if (processed < dueGameIds.Count
                && DelayBetweenItemsMs > 0
                && !ct.IsCancellationRequested)
            {
                await Task.Delay(DelayBetweenItemsMs, ct).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "WikidataCoverEnrichmentJob: tick complete — processed {Processed}/{Total} games.",
            processed, dueGameIds.Count);
    }

    private async Task ProcessGameAsync(
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IUnitOfWork uow,
        IMediator mediator,
        IWikidataCoverEnrichmentRetryPolicy policy,
        Guid gameId,
        CancellationToken ct)
    {
        var previous = await attempts
            .GetLatestBySharedGameIdAsync(gameId, ct)
            .ConfigureAwait(false);

        var previousRetryCount = previous?.RetryCount ?? 0;

        var result = await mediator
            .Send(new EnrichCatalogCoverCommand(gameId), ct)
            .ConfigureAwait(false);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var decision = policy.Classify(result, previousRetryCount, nowUtc);

        // RetryCount for the NEW attempt row:
        // - Terminal/DeadLetter: preserve previous count (this attempt is the
        //   nth retry that produced the terminal outcome).
        // - ScheduleRetry: increment by 1 (this attempt counts towards the budget).
        var nextRetryCount = decision switch
        {
            WikidataCoverEnrichmentRetryDecision.ScheduleRetry => previousRetryCount + 1,
            _ => previousRetryCount,
        };

        WikidataCoverEnrichmentAttempt newAttempt = (decision, result) switch
        {
            (WikidataCoverEnrichmentRetryDecision.Terminal, EnrichCatalogCoverResult.Success) =>
                WikidataCoverEnrichmentAttempt.RecordSuccess(gameId, nextRetryCount, nowUtc),

            (WikidataCoverEnrichmentRetryDecision.Terminal, EnrichCatalogCoverResult.Skipped skipped) =>
                WikidataCoverEnrichmentAttempt.RecordSkipped(gameId, skipped.Reason, nextRetryCount, nowUtc),

            (WikidataCoverEnrichmentRetryDecision.ScheduleRetry retry, EnrichCatalogCoverResult.Failed failed) =>
                WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
                    gameId, failed.Reason, failed.Details, nextRetryCount, nowUtc, retry.NextRetryAt),

            (WikidataCoverEnrichmentRetryDecision.DeadLetter, EnrichCatalogCoverResult.Failed failed) =>
                WikidataCoverEnrichmentAttempt.RecordDeadLetter(
                    gameId, failed.Reason, failed.Details, nextRetryCount, nowUtc),

            // Defensive: a mismatched (decision, result) pair shouldn't happen since
            // the policy only emits ScheduleRetry/DeadLetter for Failed results, but
            // record a DeadLetter rather than crash the batch.
            _ => WikidataCoverEnrichmentAttempt.RecordDeadLetter(
                gameId, "unexpected-decision", $"{decision.GetType().Name} for {result.GetType().Name}", nextRetryCount, nowUtc),
        };

        await attempts.AddAsync(newAttempt, ct).ConfigureAwait(false);
        await uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogDebug(
            "WikidataCoverEnrichmentJob: game {GameId} outcome={Outcome} reason={Reason} retry={RetryCount} nextRetryAt={NextRetryAt}",
            gameId, newAttempt.Outcome, newAttempt.Reason, newAttempt.RetryCount, newAttempt.NextRetryAt);
    }
}
