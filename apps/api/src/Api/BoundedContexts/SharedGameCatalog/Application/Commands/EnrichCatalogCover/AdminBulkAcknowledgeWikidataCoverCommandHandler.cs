using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Issue #1823 Phase F F5 (sub-issue #2254) — bulk-acknowledge dead-letter
/// attempts. Hydrates each id via <see cref="IWikidataCoverEnrichmentAttemptRepository.GetByIdsAsync"/>,
/// invokes the in-domain <see cref="WikidataCoverEnrichmentAttempt.Acknowledge"/>
/// mutator (idempotent on re-call) and persists via <see cref="IUnitOfWork"/>.
/// Per-row outcomes are bucketed into the result envelope (acked /
/// already-acked / not-found / wrong-state) so the admin UI can render the
/// partial success/failure table.
/// </summary>
/// <remarks>
/// <para>Sequential per-row processing: each row mutation runs in-memory + a
/// single repo <c>UpdateAsync</c> call (EF change-tracking attach); the
/// batch-final <c>SaveChangesAsync</c> commits everything atomically. No
/// per-row transaction overhead.</para>
///
/// <para><see cref="IUnitOfWork.SaveChangesAsync"/> is skipped entirely when
/// no row was mutated (the all-idempotent-or-not-found path) to avoid an
/// unnecessary round-trip — mirrors the EF Core convention that a no-op
/// <c>SaveChanges</c> still costs a DB hit for the change-tracker scan.</para>
///
/// <para>Cancellation propagates via <see cref="CancellationToken.ThrowIfCancellationRequested"/>
/// inside the per-row loop so a partially-applied batch fails fast rather than
/// silently truncating the result envelope. Pre-loop bulk fetch already honors
/// the token via the repository contract.</para>
///
/// <para>The optional <see cref="AdminBulkAcknowledgeWikidataCoverCommand.Note"/>
/// is DEC-F-4 log-only — surfaced in the structured log line but NOT persisted
/// on the attempt rows.</para>
/// </remarks>
internal sealed class AdminBulkAcknowledgeWikidataCoverCommandHandler
    : ICommandHandler<AdminBulkAcknowledgeWikidataCoverCommand, AdminBulkAcknowledgeResult>
{
    private readonly IWikidataCoverEnrichmentAttemptRepository _attempts;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminBulkAcknowledgeWikidataCoverCommandHandler> _logger;

    public AdminBulkAcknowledgeWikidataCoverCommandHandler(
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IUnitOfWork uow,
        TimeProvider timeProvider,
        ILogger<AdminBulkAcknowledgeWikidataCoverCommandHandler> logger)
    {
        _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminBulkAcknowledgeResult> Handle(
        AdminBulkAcknowledgeWikidataCoverCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "AdminBulkAcknowledgeWikidataCover: user {UserId} acking {Count} attempt(s); note={Note}",
            request.TriggeredByUserId, request.AttemptIds.Count, request.Note ?? "<none>");

        var loaded = await _attempts
            .GetByIdsAsync(request.AttemptIds, cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<AdminBulkAcknowledgeRow>(request.AttemptIds.Count);
        var acked = 0;
        var alreadyAcked = 0;
        var notFound = 0;
        var wrongState = 0;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var anyUpdated = false;

        foreach (var id in request.AttemptIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!loaded.TryGetValue(id, out var attempt))
            {
                rows.Add(new AdminBulkAcknowledgeRow(
                    AttemptId: id,
                    GameId: null,
                    Outcome: AdminBulkAcknowledgeRow.OutcomeNotFound,
                    Reason: "attempt-id-not-found"));
                notFound++;
                continue;
            }

            if (attempt.Outcome != WikidataCoverEnrichmentOutcome.DeadLetter)
            {
                rows.Add(new AdminBulkAcknowledgeRow(
                    AttemptId: id,
                    GameId: attempt.SharedGameId,
                    Outcome: AdminBulkAcknowledgeRow.OutcomeWrongState,
                    Reason: $"current-outcome:{attempt.Outcome}"));
                wrongState++;
                continue;
            }

            if (attempt.AcknowledgedAt is not null)
            {
                rows.Add(new AdminBulkAcknowledgeRow(
                    AttemptId: id,
                    GameId: attempt.SharedGameId,
                    Outcome: AdminBulkAcknowledgeRow.OutcomeAlreadyAcked,
                    Reason: null));
                alreadyAcked++;
                continue;
            }

            attempt.Acknowledge(request.TriggeredByUserId, nowUtc);
            await _attempts.UpdateAsync(attempt, cancellationToken).ConfigureAwait(false);
            rows.Add(new AdminBulkAcknowledgeRow(
                AttemptId: id,
                GameId: attempt.SharedGameId,
                Outcome: AdminBulkAcknowledgeRow.OutcomeAcked,
                Reason: null));
            acked++;
            anyUpdated = true;
        }

        if (anyUpdated)
        {
            await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new AdminBulkAcknowledgeResult(
            AckedCount: acked,
            IdempotentNoOpCount: alreadyAcked,
            NotFoundCount: notFound,
            WrongStateCount: wrongState,
            Rows: rows);
    }
}
