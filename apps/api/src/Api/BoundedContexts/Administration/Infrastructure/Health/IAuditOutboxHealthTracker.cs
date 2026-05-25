namespace Api.BoundedContexts.Administration.Infrastructure.Health;

/// <summary>
/// Singleton health tracker for the audit_outbox queue. Holds the most-recent snapshot of
/// aggregate counters (Pending count, oldest-Pending age, Failed count) populated by
/// <c>AuditOutboxProcessor</c> after every batch. The ObservableGauges registered in
/// <c>MeepleAiMetrics.AuditOutbox</c> read from this tracker on metric collection.
///
/// Snapshot freshness: bounded by the processor's poll interval (5s by default). For backlog
/// alerts based on <c>oldest_pending_age_seconds</c>, this introduces at most a poll-interval
/// of lag — acceptable for the admin-audit use case.
///
/// SP5 Admin Security S1 — Task 4b.
/// </summary>
public interface IAuditOutboxHealthTracker
{
    /// <summary>
    /// Replaces the snapshot atomically. Called from <c>AuditOutboxProcessor.RunOnceAsync</c>
    /// after each batch (including empty batches, so a quiet system reports 0/0/0 instead of
    /// a stale value).
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
