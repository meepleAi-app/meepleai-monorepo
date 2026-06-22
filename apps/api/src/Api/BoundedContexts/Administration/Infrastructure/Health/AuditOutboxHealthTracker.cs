namespace Api.BoundedContexts.Administration.Infrastructure.Health;

/// <summary>
/// Default singleton implementation of <see cref="IAuditOutboxHealthTracker"/>. Uses
/// <see cref="Interlocked"/> for atomic snapshot updates so a concurrent metric collection
/// always reads a consistent value (no torn writes).
///
/// Note: the three counters are stored INDEPENDENTLY — there is no cross-counter consistency
/// guarantee (e.g. a collector may observe pendingCount from snapshot N and failedCount from
/// snapshot N+1 if it scrapes mid-update). This is intentional and acceptable: metrics are
/// observability data, not transactional state, and the bounded staleness window (≤5s poll)
/// dominates any per-counter skew.
/// </summary>
internal sealed class AuditOutboxHealthTracker : IAuditOutboxHealthTracker
{
    private long _pendingCount;
    private long _failedCount;
    // double cannot be passed to Interlocked.Exchange; round-trip via long bit pattern.
    private long _oldestPendingAgeSecondsBits;

    public void RecordSnapshot(long pendingCount, double oldestPendingAgeSeconds, long failedCount)
    {
        Interlocked.Exchange(ref _pendingCount, pendingCount);
        Interlocked.Exchange(ref _failedCount, failedCount);
        Interlocked.Exchange(
            ref _oldestPendingAgeSecondsBits,
            BitConverter.DoubleToInt64Bits(oldestPendingAgeSeconds));
    }

    public long GetPendingCount() => Interlocked.Read(ref _pendingCount);

    public long GetFailedCount() => Interlocked.Read(ref _failedCount);

    public double GetOldestPendingAgeSeconds()
        => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _oldestPendingAgeSecondsBits));
}
