using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Observability;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Default implementation of <see cref="IWikidataCoverEnrichmentRunner"/>.
/// Single source of truth for the enrich+record workflow, consumed by
/// the M9 <c>WikidataCoverEnrichmentJob</c> scheduler and the M12 admin
/// trigger endpoint. Issue #1823 Wave 3 M12.
/// </summary>
internal sealed class WikidataCoverEnrichmentRunner : IWikidataCoverEnrichmentRunner
{
    private readonly IMediator _mediator;
    private readonly IWikidataCoverEnrichmentAttemptRepository _attempts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWikidataCoverEnrichmentRetryPolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly IWikidataEnrichmentEventBroadcaster _broadcaster;
    private readonly ILogger<WikidataCoverEnrichmentRunner> _logger;

    public WikidataCoverEnrichmentRunner(
        IMediator mediator,
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IUnitOfWork unitOfWork,
        IWikidataCoverEnrichmentRetryPolicy policy,
        TimeProvider timeProvider,
        IWikidataEnrichmentEventBroadcaster broadcaster,
        ILogger<WikidataCoverEnrichmentRunner> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnrichCatalogCoverResult> EnrichAndRecordAsync(
        Guid gameId,
        bool forceRefresh,
        Guid? triggeredByAdminUserId = null,
        CancellationToken cancellationToken = default)
    {
        var previous = await _attempts
            .GetLatestBySharedGameIdAsync(gameId, cancellationToken)
            .ConfigureAwait(false);

        var previousRetryCount = previous?.RetryCount ?? 0;

        var result = await _mediator
            .Send(new EnrichCatalogCoverCommand(gameId, forceRefresh), cancellationToken)
            .ConfigureAwait(false);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var decision = _policy.Classify(result, previousRetryCount, nowUtc);

        // RetryCount for the NEW attempt row:
        // - Terminal / DeadLetter: preserve previous count (this attempt is the
        //   nth retry that produced the terminal outcome).
        // - ScheduleRetry with reason=circuit-open (Wave 3 M13 / M10 follow-up):
        //   preserve previous count. A breaker trip is upstream infrastructure,
        //   not a per-game failure, so we don't want to burn the DEC-3j 3-retry
        //   budget on it.
        // - ScheduleRetry with any other reason: increment by 1 (this attempt
        //   counts towards the budget).
        var nextRetryCount = decision switch
        {
            WikidataCoverEnrichmentRetryDecision.ScheduleRetry
                when result is EnrichCatalogCoverResult.Failed { Reason: EnrichCatalogCoverCommandHandler.FailReasonCircuitOpen }
                => previousRetryCount,
            WikidataCoverEnrichmentRetryDecision.ScheduleRetry => previousRetryCount + 1,
            _ => previousRetryCount,
        };

        // Issue #1823 Phase F F6: thread the triggering admin id into every
        // factory variant so attempt rows persist the trigger source (M9 cron =
        // null, M12 admin = id, F2 bulk-retry = id). The default null on the
        // factory signatures keeps existing callers backward-compatible.
        WikidataCoverEnrichmentAttempt newAttempt = (decision, result) switch
        {
            (WikidataCoverEnrichmentRetryDecision.Terminal, EnrichCatalogCoverResult.Success) =>
                WikidataCoverEnrichmentAttempt.RecordSuccess(gameId, nextRetryCount, nowUtc, triggeredByAdminUserId),

            (WikidataCoverEnrichmentRetryDecision.Terminal, EnrichCatalogCoverResult.Skipped skipped) =>
                WikidataCoverEnrichmentAttempt.RecordSkipped(gameId, skipped.Reason, nextRetryCount, nowUtc, triggeredByAdminUserId),

            (WikidataCoverEnrichmentRetryDecision.ScheduleRetry retry, EnrichCatalogCoverResult.Failed failed) =>
                WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
                    gameId, failed.Reason, failed.Details, nextRetryCount, nowUtc, retry.NextRetryAt,
                    triggeredByAdminUserId),

            (WikidataCoverEnrichmentRetryDecision.DeadLetter, EnrichCatalogCoverResult.Failed failed) =>
                WikidataCoverEnrichmentAttempt.RecordDeadLetter(
                    gameId, failed.Reason, failed.Details, nextRetryCount, nowUtc, triggeredByAdminUserId),

            // Defensive: a mismatched (decision, result) pair shouldn't happen
            // because the policy only emits ScheduleRetry/DeadLetter for Failed
            // results, but record a DeadLetter rather than crash the runner.
            _ => WikidataCoverEnrichmentAttempt.RecordDeadLetter(
                gameId, "unexpected-decision",
                $"{decision.GetType().Name} for {result.GetType().Name}",
                nextRetryCount, nowUtc, triggeredByAdminUserId),
        };

        await _attempts.AddAsync(newAttempt, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #1823 Wave 3 F1: increment the dead-letter gauge AFTER the
        // SaveChanges commits — incrementing pre-save would leak the metric
        // forward on a DB write failure. The retention job re-anchors the
        // gauge daily so any drift between sweeps stays bounded.
        if (newAttempt.Outcome == WikidataCoverEnrichmentOutcome.DeadLetter)
        {
            MeepleAiMetrics.IncrementWikidataDeadLetterCount();
        }

        // Issue #1823 Phase E F4: SSE broadcast for live admin dead-letter
        // page updates. Same post-SaveChanges placement as the F1 gauge — a
        // pre-commit publish would leak phantom rows if the DB write fails.
        // The broadcaster is fire-and-forget; a slow SSE subscriber is
        // dropped rather than back-pressured (per its DropOldest channel
        // policy) so the M9 scheduler tick budget is preserved.
        //
        // F6 (Phase F): the payload includes TriggeredByAdminUserId so the
        // admin SSE consumer can update the row inline; TriggeredByAdminFullName
        // is intentionally null here (resolved in the F6 timeline query path
        // via LEFT JOIN — the broadcaster cannot afford a per-publish DB lookup
        // without breaking the DEC-3e scheduler tick budget).
        _broadcaster.Publish(new WikidataEnrichmentEvent(
            AttemptId: newAttempt.Id,
            SharedGameId: newAttempt.SharedGameId,
            Outcome: newAttempt.Outcome.ToString(),
            Reason: newAttempt.Reason,
            AttemptedAt: newAttempt.AttemptedAt,
            RetryCount: newAttempt.RetryCount,
            NextRetryAt: newAttempt.NextRetryAt,
            DeadLetteredAt: newAttempt.DeadLetteredAt,
            TriggeredByAdminUserId: newAttempt.TriggeredByAdminUserId,
            TriggeredByAdminFullName: null));

        _logger.LogDebug(
            "WikidataCoverEnrichmentRunner: game {GameId} outcome={Outcome} reason={Reason} retry={RetryCount} nextRetryAt={NextRetryAt} forceRefresh={ForceRefresh} triggeredByAdmin={TriggeredByAdminUserId}",
            gameId, newAttempt.Outcome, newAttempt.Reason, newAttempt.RetryCount, newAttempt.NextRetryAt, forceRefresh, newAttempt.TriggeredByAdminUserId);

        return result;
    }
}
