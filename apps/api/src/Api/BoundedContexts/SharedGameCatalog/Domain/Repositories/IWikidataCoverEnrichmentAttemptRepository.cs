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

    /// <summary>
    /// Issue #1823 Wave 3 F1 (M11 follow-up) — returns the current count of
    /// dead-letter rows present in the table. Used by the retention job to
    /// re-anchor the <c>meepleai.wikidata.dead_letter_count</c> ObservableGauge
    /// after each daily sweep.
    /// </summary>
    Task<int> CountDeadLettersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #1823 Wave 3 M13 — returns a page of dead-letter attempt rows
    /// joined with the parent <c>SharedGame.Title</c> for the admin
    /// dead-letter visibility page. Filtered by optional reason; ordered by
    /// most-recently dead-lettered first.
    /// </summary>
    /// <param name="skip">Skip count for pagination (must be &gt;= 0).</param>
    /// <param name="take">Page size (1..200; clamped).</param>
    /// <param name="reasonFilter">Optional <c>Reason</c> exact match; null returns all dead-letters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Page items (with denormalized game title) + total count of dead-letter rows matching the filter.</returns>
    Task<DeadLetterPage> GetDeadLettersAsync(
        int skip,
        int take,
        string? reasonFilter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #1823 Phase E F2 — resolves the supplied attempt ids to their
    /// parent <c>SharedGameId</c> in a single round-trip. Missing ids are
    /// simply absent from the dictionary so the caller can report
    /// <c>not-found</c> to the admin UI.
    /// </summary>
    /// <param name="attemptIds">Set of attempt ids to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Map <c>attemptId → SharedGameId</c>; absent keys = attempt not found.</returns>
    Task<IReadOnlyDictionary<Guid, Guid>> GetSharedGameIdsByAttemptIdsAsync(
        IReadOnlyCollection<Guid> attemptIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #1823 Phase E F3 — returns the full attempt timeline for a single
    /// game, ordered by <c>AttemptedAt</c> DESC. Bounded by <paramref name="limit"/>
    /// (clamped to a sane upper bound by the impl) so the admin drawer never
    /// pulls thousands of rows.
    /// </summary>
    /// <param name="sharedGameId">Target game id.</param>
    /// <param name="limit">Max rows to return (clamped to [1, 200]).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<WikidataCoverEnrichmentAttempt>> GetAttemptsByGameIdAsync(
        Guid sharedGameId,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of <see cref="IWikidataCoverEnrichmentAttemptRepository.GetDeadLettersAsync"/>.
/// </summary>
/// <param name="Items">Page of dead-letter rows with denormalized parent game title.</param>
/// <param name="TotalCount">Total dead-letter row count matching the filter (for paginator).</param>
public sealed record DeadLetterPage(
    IReadOnlyList<DeadLetterRow> Items,
    int TotalCount);

/// <summary>
/// Per-row admin DTO returned by <see cref="IWikidataCoverEnrichmentAttemptRepository.GetDeadLettersAsync"/>.
/// </summary>
public sealed record DeadLetterRow(
    Guid Id,
    Guid SharedGameId,
    string GameTitle,
    DateTime AttemptedAt,
    DateTime DeadLetteredAt,
    string Reason,
    string? Details,
    int RetryCount);
