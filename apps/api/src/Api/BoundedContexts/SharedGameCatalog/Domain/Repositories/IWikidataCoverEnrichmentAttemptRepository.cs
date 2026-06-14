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
    /// <remarks>
    /// Phase F (#2254) added <paramref name="includeAcknowledged"/>: when
    /// <see langword="false"/> (the default behavior for the open-work view),
    /// rows whose <c>AcknowledgedAt</c> is non-null are filtered out so
    /// operators don't see already-triaged dead-letters. The partial index
    /// <c>ix_wikidata_cover_attempts_acknowledged_at</c> (defined in the EF
    /// config) keeps this filter cheap.
    /// </remarks>
    /// <param name="skip">Skip count for pagination (must be &gt;= 0).</param>
    /// <param name="take">Page size (1..200; clamped).</param>
    /// <param name="reasonFilter">Optional <c>Reason</c> exact match; null returns all dead-letters.</param>
    /// <param name="includeAcknowledged">When false, hide rows with <c>AcknowledgedAt</c> set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Page items (with denormalized game title + ack/trigger admin names) + total count matching the filter.</returns>
    Task<DeadLetterPage> GetDeadLettersAsync(
        int skip,
        int take,
        string? reasonFilter,
        bool includeAcknowledged,
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
    /// pulls thousands of rows. Phase F (#2255) extended the row shape to
    /// include F6 admin attribution via a LEFT JOIN on Users.
    /// </summary>
    /// <param name="sharedGameId">Target game id.</param>
    /// <param name="limit">Max rows to return (clamped to [1, 200]).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<WikidataAttemptTimelineRow>> GetAttemptsByGameIdAsync(
        Guid sharedGameId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #2254 (Phase F F5) — bulk-loads aggregates by id for the
    /// bulk-acknowledge handler. Missing ids are simply absent from the result
    /// dictionary so the caller can report not-found rows to the admin UI.
    /// Caller must invoke <c>IUnitOfWork.SaveChangesAsync</c> after mutating
    /// the returned aggregates (see <see cref="UpdateAsync"/>).
    /// </summary>
    /// <param name="attemptIds">Set of attempt ids to resolve (defensive cap of 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Map <c>attemptId → aggregate</c>; absent keys = attempt not found.</returns>
    Task<IReadOnlyDictionary<Guid, WikidataCoverEnrichmentAttempt>> GetByIdsAsync(
        IReadOnlyCollection<Guid> attemptIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #2254 (Phase F F5) — persists mutations performed on a previously
    /// hydrated aggregate. Per the record-of-fact pattern only the ack metadata
    /// pair (<c>AcknowledgedAt</c>, <c>AcknowledgedBy</c>) is mutable; other
    /// fields are intentionally not copied back. Caller must invoke
    /// <c>IUnitOfWork.SaveChangesAsync</c>.
    /// </summary>
    Task UpdateAsync(
        WikidataCoverEnrichmentAttempt attempt,
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
/// Phase F extended with F5 ack metadata and F6 admin attribution.
/// </summary>
/// <param name="Id">Attempt id.</param>
/// <param name="SharedGameId">Parent shared game id.</param>
/// <param name="GameTitle">Denormalized parent title (fallback "(deleted game)").</param>
/// <param name="AttemptedAt">UTC attempt timestamp.</param>
/// <param name="DeadLetteredAt">UTC dead-letter timestamp (guaranteed non-null on this row).</param>
/// <param name="Reason">Stable machine-readable reason.</param>
/// <param name="Details">Optional human-readable detail.</param>
/// <param name="RetryCount">0..N counter from the original pipeline.</param>
/// <param name="AcknowledgedAt">F5 — UTC ack timestamp (null if not acknowledged).</param>
/// <param name="AcknowledgedBy">F5 — User id who acknowledged (null if not acknowledged).</param>
/// <param name="AcknowledgedByFullName">F5 — display name of the acknowledging user (null if not acknowledged or user deleted).</param>
/// <param name="TriggeredByAdminUserId">F6 — admin user id that triggered this attempt via M12/F2 (null for M9 scheduler).</param>
/// <param name="TriggeredByAdminFullName">F6 — display name of the triggering admin (null when M9 scheduler or user deleted).</param>
public sealed record DeadLetterRow(
    Guid Id,
    Guid SharedGameId,
    string GameTitle,
    DateTime AttemptedAt,
    DateTime DeadLetteredAt,
    string Reason,
    string? Details,
    int RetryCount,
    DateTime? AcknowledgedAt,
    Guid? AcknowledgedBy,
    string? AcknowledgedByFullName,
    Guid? TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);

/// <summary>
/// Issue #1823 Phase E F3 timeline row returned by
/// <see cref="IWikidataCoverEnrichmentAttemptRepository.GetAttemptsByGameIdAsync"/>.
/// Mirrors the aggregate shape but adds the F6 admin LEFT JOIN result so the
/// admin drawer can render attribution without a second round-trip.
/// </summary>
/// <param name="Id">Attempt id.</param>
/// <param name="AttemptedAt">UTC attempt timestamp.</param>
/// <param name="Outcome">Outcome category.</param>
/// <param name="Reason">Stable machine-readable reason.</param>
/// <param name="Details">Optional human-readable detail.</param>
/// <param name="RetryCount">Counter from the original pipeline iteration.</param>
/// <param name="NextRetryAt">UTC retry schedule (null for terminal outcomes).</param>
/// <param name="DeadLetteredAt">UTC dead-letter timestamp (null unless DeadLetter outcome).</param>
/// <param name="TriggeredByAdminUserId">F6 — admin user id that triggered the attempt (null for M9 scheduler).</param>
/// <param name="TriggeredByAdminFullName">F6 — display name of the triggering admin (null when M9 scheduler or user deleted).</param>
public sealed record WikidataAttemptTimelineRow(
    Guid Id,
    DateTime AttemptedAt,
    WikidataCoverEnrichmentOutcome Outcome,
    string Reason,
    string? Details,
    int RetryCount,
    DateTime? NextRetryAt,
    DateTime? DeadLetteredAt,
    Guid? TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);
