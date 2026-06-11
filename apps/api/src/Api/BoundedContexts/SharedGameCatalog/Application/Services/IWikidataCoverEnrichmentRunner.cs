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
    /// <param name="cancellationToken">Cancellation token honoured by all downstream calls.</param>
    /// <returns>The terminal outcome of the underlying <see cref="EnrichCatalogCoverCommand"/>.</returns>
    Task<EnrichCatalogCoverResult> EnrichAndRecordAsync(
        Guid gameId,
        bool forceRefresh,
        CancellationToken cancellationToken = default);
}
