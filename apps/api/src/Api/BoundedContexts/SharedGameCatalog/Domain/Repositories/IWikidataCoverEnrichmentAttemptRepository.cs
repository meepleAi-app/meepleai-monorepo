using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Persistence interface for <see cref="WikidataCoverEnrichmentAttempt"/>.
/// Issue #1823 Wave 3 M9.
/// </summary>
public interface IWikidataCoverEnrichmentAttemptRepository
{
    /// <summary>Appends a new attempt row. Caller must invoke <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    Task AddAsync(WikidataCoverEnrichmentAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Latest attempt per game by <see cref="WikidataCoverEnrichmentAttempt.AttemptedAt"/> DESC.
    /// Returns <see langword="null"/> when the game has never been processed.
    /// </summary>
    Task<WikidataCoverEnrichmentAttempt?> GetLatestBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> shared-game IDs that are ready to be
    /// enriched by the M9 scheduler — either never attempted OR scheduled for retry
    /// at or before <paramref name="nowUtc"/>. Terminal outcomes (Success / Skipped
    /// / DeadLetter) are excluded. Result is ordered by oldest game first to give
    /// long-tail catalog entries a fair share of the rate-limit budget.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetGameIdsDueForEnrichmentAsync(
        int limit,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #1823 Wave 3 retention sweep (ADR DEC-3j: dead-letter retention 7
    /// days). Deletes every attempt row whose <c>DeadLetteredAt</c> is non-null
    /// AND strictly less than <paramref name="cutoffUtc"/>. Live attempt rows
    /// (Success / Skipped / pending retries) are untouched.
    /// </summary>
    /// <param name="cutoffUtc">UTC threshold; rows older than this are deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows physically removed from the table.</returns>
    Task<int> DeleteDeadLetteredOlderThanAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);
}
