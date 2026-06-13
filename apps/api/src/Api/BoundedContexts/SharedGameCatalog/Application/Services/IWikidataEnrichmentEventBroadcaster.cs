namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #1823 Phase E F4 — in-process pub/sub for Wikidata cover enrichment
/// outcomes. The M9 <see cref="IWikidataCoverEnrichmentRunner"/> publishes one
/// event per persisted <c>WikidataCoverEnrichmentAttempt</c>; the admin
/// dead-letter visibility page subscribes via SSE for live row updates.
/// </summary>
/// <remarks>
/// <para><b>Topology assumption</b>: single-pod (HPA=1, per DEC-3e rate-limiter
/// contract). A multi-pod deployment would need a fan-out backplane (Redis
/// pub/sub or NATS) — explicitly out of scope until DEC-3e is revisited.</para>
///
/// <para><b>Delivery semantics</b>: at-most-once, best-effort. Slow / paused
/// subscribers do NOT back-pressure the publisher — the implementation drops
/// the event for that subscriber and continues. This preserves the M9
/// scheduler tick budget; the dead-letter table remains the source of truth.</para>
/// </remarks>
public interface IWikidataEnrichmentEventBroadcaster
{
    /// <summary>
    /// Fire-and-forget publish. Returns immediately; per-subscriber delivery
    /// happens on a background continuation.
    /// </summary>
    void Publish(WikidataEnrichmentEvent payload);

    /// <summary>
    /// Subscribe to the live event stream. The returned async-enumerable
    /// completes when <paramref name="cancellationToken"/> is signalled
    /// (typically the SSE client disconnecting).
    /// </summary>
    IAsyncEnumerable<WikidataEnrichmentEvent> SubscribeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Current number of subscribers — surfaced as an admin diagnostic and used
    /// by unit tests to assert subscribe / unsubscribe behaviour.
    /// </summary>
    int SubscriberCount { get; }
}

/// <summary>
/// Wire payload for a single attempt-recorded SSE event. Mirrors the slim
/// projection of <see cref="Domain.Aggregates.WikidataCoverEnrichmentAttempt"/>
/// needed to refresh the admin dead-letter row inline.
/// </summary>
/// <param name="AttemptId">Stable id of the persisted attempt row.</param>
/// <param name="SharedGameId">Target shared-game aggregate id.</param>
/// <param name="Outcome">Stringified <see cref="Domain.Aggregates.WikidataCoverEnrichmentOutcome"/> name.</param>
/// <param name="Reason">Stable machine-readable reason; <see langword="null"/> on success.</param>
/// <param name="AttemptedAt">UTC timestamp when the attempt ran.</param>
/// <param name="RetryCount">0-based retry count for this attempt row.</param>
/// <param name="NextRetryAt">UTC retry schedule; <see langword="null"/> on terminal outcomes.</param>
/// <param name="DeadLetteredAt">UTC dead-letter timestamp; <see langword="null"/> unless <see cref="Outcome"/> is <c>DeadLetter</c>.</param>
/// <param name="TriggeredByAdminUserId">
/// Issue #1823 Phase F F6 — user id of the admin who triggered this attempt via
/// M12 or F2; <see langword="null"/> when the M9 scheduler authored it. The
/// broadcaster only knows the id (resolving to a full name would require a DB
/// LEFT JOIN per publish, which is incompatible with the synchronous fire-and-forget
/// contract and the DEC-3e scheduler tick budget). Subscribers that need the
/// display name fetch it via the F6 timeline query path.
/// </param>
/// <param name="TriggeredByAdminFullName">
/// Always <see langword="null"/> on the broadcast payload — resolved server-side
/// in the F6 timeline query via LEFT JOIN. Retained on the wire schema so
/// subscribers see a stable shape and can degrade gracefully when the FullName
/// is unknown.
/// </param>
public sealed record WikidataEnrichmentEvent(
    Guid AttemptId,
    Guid SharedGameId,
    string Outcome,
    string? Reason,
    DateTime AttemptedAt,
    int RetryCount,
    DateTime? NextRetryAt,
    DateTime? DeadLetteredAt,
    Guid? TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);
