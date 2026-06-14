using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Encapsulates the "send <see cref="EnrichCatalogCoverCommand"/> + classify
/// outcome via <see cref="IWikidataCoverEnrichmentRetryPolicy"/> + record the
/// resulting <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.WikidataCoverEnrichmentAttempt"/>"
/// workflow used both by the M9 scheduler tick and the M12 admin trigger
/// endpoint. Single source of truth so the audit trail is identical regardless
/// of who pulled the trigger. Issue #1823 Wave 3 M12.
/// </summary>
internal interface IWikidataCoverEnrichmentRunner
{
    /// <summary>
    /// Runs the enrichment pipeline for one game and persists a new attempt
    /// row reflecting the outcome.
    /// </summary>
    /// <param name="gameId">Target shared-game id.</param>
    /// <param name="forceRefresh">
    /// When <see langword="true"/>, the M8 handler bypasses the 90-day freshness
    /// window (admin dogfood / edge-case re-runs); the scheduler always passes
    /// <see langword="false"/>.
    /// </param>
    /// <param name="triggeredByAdminUserId">
    /// Issue #1823 Phase F F6 — user id of the admin who triggered this attempt
    /// (M12 single trigger or F2 bulk retry). Stamped onto the persisted
    /// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.WikidataCoverEnrichmentAttempt.TriggeredByAdminUserId"/>
    /// and on the SSE broadcast payload so the admin timeline drawer + dead-letter
    /// page can show "triggered by admin X" without a separate audit table.
    /// Defaults to <see langword="null"/> for the M9 scheduler tick (the dominant
    /// caller); admin callers MUST pass their user id explicitly.
    /// </param>
    /// <param name="cancellationToken">Cancellation token honoured by all downstream calls.</param>
    /// <returns>The terminal outcome of the underlying <see cref="EnrichCatalogCoverCommand"/>.</returns>
    Task<EnrichCatalogCoverResult> EnrichAndRecordAsync(
        Guid gameId,
        bool forceRefresh,
        Guid? triggeredByAdminUserId = null,
        CancellationToken cancellationToken = default);
}
