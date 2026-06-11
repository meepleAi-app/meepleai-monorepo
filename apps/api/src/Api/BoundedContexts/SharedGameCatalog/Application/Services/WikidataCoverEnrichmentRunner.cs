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
    private readonly ILogger<WikidataCoverEnrichmentRunner> _logger;

    public WikidataCoverEnrichmentRunner(
        IMediator mediator,
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IUnitOfWork unitOfWork,
        IWikidataCoverEnrichmentRetryPolicy policy,
        TimeProvider timeProvider,
        ILogger<WikidataCoverEnrichmentRunner> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnrichCatalogCoverResult> EnrichAndRecordAsync(
        Guid gameId,
        bool forceRefresh,
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

            // Defensive: a mismatched (decision, result) pair shouldn't happen
            // because the policy only emits ScheduleRetry/DeadLetter for Failed
            // results, but record a DeadLetter rather than crash the runner.
            _ => WikidataCoverEnrichmentAttempt.RecordDeadLetter(
                gameId, "unexpected-decision",
                $"{decision.GetType().Name} for {result.GetType().Name}",
                nextRetryCount, nowUtc),
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

        _logger.LogDebug(
            "WikidataCoverEnrichmentRunner: game {GameId} outcome={Outcome} reason={Reason} retry={RetryCount} nextRetryAt={NextRetryAt} forceRefresh={ForceRefresh}",
            gameId, newAttempt.Outcome, newAttempt.Reason, newAttempt.RetryCount, newAttempt.NextRetryAt, forceRefresh);

        return result;
    }
}
