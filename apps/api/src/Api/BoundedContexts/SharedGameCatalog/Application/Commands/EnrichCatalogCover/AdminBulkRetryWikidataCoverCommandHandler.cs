using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Handler for <see cref="AdminBulkRetryWikidataCoverCommand"/>. Resolves each
/// attempt id to its parent <see cref="Domain.Aggregates.WikidataCoverEnrichmentAttempt.SharedGameId"/>,
/// then re-dispatches via <see cref="IWikidataCoverEnrichmentRunner"/> with
/// <c>forceRefresh=true</c>. Per-row failures are caught and converted into a
/// <c>failed</c> envelope so a single broken game never breaks the batch.
/// Issue #1823 Phase E F2.
/// </summary>
/// <remarks>
/// <para>Sequential dispatch (no parallel fan-out): the runner ultimately calls
/// the DEC-3e shared rate-limiter (5rps across SPARQL + Commons); parallel
/// dispatch would burn rate-limit budget without speeding the batch up.</para>
///
/// <para>The per-row <c>try/catch</c> swallows exceptions intentionally — the
/// handler MUST always return a result envelope so the admin UI can render the
/// partial success/failure table. Background runner errors are still logged
/// via <see cref="ILogger"/> for ops triage.</para>
/// </remarks>
internal sealed class AdminBulkRetryWikidataCoverCommandHandler
    : ICommandHandler<AdminBulkRetryWikidataCoverCommand, AdminBulkRetryWikidataCoverResult>
{
    private readonly IWikidataCoverEnrichmentAttemptRepository _attempts;
    private readonly IWikidataCoverEnrichmentRunner _runner;
    private readonly ILogger<AdminBulkRetryWikidataCoverCommandHandler> _logger;

    public AdminBulkRetryWikidataCoverCommandHandler(
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IWikidataCoverEnrichmentRunner runner,
        ILogger<AdminBulkRetryWikidataCoverCommandHandler> logger)
    {
        _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminBulkRetryWikidataCoverResult> Handle(
        AdminBulkRetryWikidataCoverCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "AdminBulkRetryWikidataCover: user {UserId} requested bulk retry of {Count} attempt(s)",
            request.TriggeredByUserId, request.AttemptIds.Count);

        var gameIdMap = await _attempts
            .GetSharedGameIdsByAttemptIdsAsync(request.AttemptIds, cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<AdminBulkRetryRow>(request.AttemptIds.Count);
        var triggered = 0;
        var notFound = 0;
        var failed = 0;

        foreach (var attemptId in request.AttemptIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!gameIdMap.TryGetValue(attemptId, out var gameId))
            {
                rows.Add(new AdminBulkRetryRow(
                    AttemptId: attemptId,
                    GameId: null,
                    Outcome: AdminBulkRetryRow.OutcomeNotFound,
                    Reason: "attempt-id-not-found"));
                notFound++;
                continue;
            }

            try
            {
                // Issue #1823 Phase F F6: pass the bulk-retry initiator id to
                // the runner so each re-dispatched attempt row records the
                // admin who pressed the button (mirror of M12 single-trigger).
                await _runner
                    .EnrichAndRecordAsync(
                        gameId,
                        forceRefresh: true,
                        triggeredByAdminUserId: request.TriggeredByUserId,
                        cancellationToken)
                    .ConfigureAwait(false);

                rows.Add(new AdminBulkRetryRow(
                    AttemptId: attemptId,
                    GameId: gameId,
                    Outcome: AdminBulkRetryRow.OutcomeTriggered,
                    Reason: null));
                triggered++;
            }
            catch (OperationCanceledException)
            {
                // Cancellation propagates — let the outer pipeline handle it
                // so the partial batch is not silently truncated into a
                // misleading result envelope.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AdminBulkRetryWikidataCover: runner threw for attempt {AttemptId} (game {GameId})",
                    attemptId, gameId);
                rows.Add(new AdminBulkRetryRow(
                    AttemptId: attemptId,
                    GameId: gameId,
                    Outcome: AdminBulkRetryRow.OutcomeFailed,
                    Reason: ex.GetType().Name));
                failed++;
            }
        }

        return new AdminBulkRetryWikidataCoverResult(
            TriggeredCount: triggered,
            NotFoundCount: notFound,
            FailedCount: failed,
            Rows: rows);
    }
}
