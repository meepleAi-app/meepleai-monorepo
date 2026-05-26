namespace Api.BoundedContexts.Administration.Infrastructure.Health;

/// <summary>
/// Singleton health tracker for active impersonation sessions. Holds the most-recent count
/// of active impersonations, refreshed periodically by <c>ImpersonationMetricsRefreshService</c>.
/// The ObservableGauge registered in <c>MeepleAiMetrics.Impersonation</c> reads from this tracker
/// on metric collection.
///
/// Design (mirrors S1 T4b <c>IAuditOutboxHealthTracker</c>): the count is a periodic snapshot
/// (refresh interval ≈ 30s), NOT a live increment/decrement. A real query is the source of truth,
/// which is robust against process restarts and the three different lifecycle exits
/// (self-end, superadmin revoke, middleware auto-expiry) that would each have to remember to
/// decrement a live counter.
///
/// SP5 Admin Security S2 — Task 7 / D-S2-5 oversight.
/// </summary>
public interface IImpersonationHealthTracker
{
    /// <summary>Replaces the active-count snapshot. Called by the periodic refresh service.</summary>
    void SetActiveCount(long activeCount);

    /// <summary>Current active impersonation count. Returns 0 before the first refresh.</summary>
    long GetActiveCount();
}
