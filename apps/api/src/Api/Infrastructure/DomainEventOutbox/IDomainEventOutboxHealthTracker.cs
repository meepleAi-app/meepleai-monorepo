namespace Api.Infrastructure.DomainEventOutbox;

/// <summary>
/// Singleton health tracker for the domain_event_outbox queue (issue #1535).
///
/// <para>Holds the most-recent snapshot of aggregate counters (Pending count,
/// oldest-Pending age, Failed count) populated by <c>DomainEventOutboxProcessor</c>
/// after every batch. The ObservableGauges registered in
/// <c>MeepleAiMetrics.DomainEventOutbox</c> read from this tracker on metric
/// collection.</para>
///
/// <para>Snapshot freshness: bounded by the processor's poll interval (5s by default).
/// For backlog alerts based on <c>oldest_pending_age_seconds</c>, this introduces at
/// most a poll-interval of lag — acceptable for the post-commit dispatch use case.</para>
///
/// <para>Mirror of <c>IAuditOutboxHealthTracker</c> (PR #1532). Kept separate so the two
/// outboxes can ship independent metric labels / alert thresholds without coupling.</para>
/// </summary>
public interface IDomainEventOutboxHealthTracker
{
    /// <summary>
    /// Replaces the snapshot atomically. Called from <c>DomainEventOutboxProcessor</c>
    /// after each batch (including empty batches, so a quiet system reports 0/0/0
    /// instead of a stale value).
    /// </summary>
    void RecordSnapshot(long pendingCount, double oldestPendingAgeSeconds, long failedCount);

    /// <summary>
    /// Current Pending row count. Returns 0 when no snapshot has been recorded yet.
    /// </summary>
    long GetPendingCount();

    /// <summary>
    /// Age in seconds of the oldest Pending row at the time of the last snapshot. Returns 0
    /// when the queue was empty or no snapshot has been recorded yet.
    /// </summary>
    double GetOldestPendingAgeSeconds();

    /// <summary>
    /// Current Failed row count. Returns 0 when no snapshot has been recorded yet.
    /// </summary>
    long GetFailedCount();
}
