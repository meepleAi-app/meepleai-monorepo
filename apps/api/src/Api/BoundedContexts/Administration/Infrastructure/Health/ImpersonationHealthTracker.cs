namespace Api.BoundedContexts.Administration.Infrastructure.Health;

/// <summary>
/// Default singleton implementation of <see cref="IImpersonationHealthTracker"/>. Uses
/// <see cref="Interlocked"/> for atomic snapshot updates so a concurrent metric collection always
/// reads a consistent value. Mirrors S1 T4b <c>AuditOutboxHealthTracker</c>.
/// </summary>
internal sealed class ImpersonationHealthTracker : IImpersonationHealthTracker
{
    private long _activeCount;

    public void SetActiveCount(long activeCount) => Interlocked.Exchange(ref _activeCount, activeCount);

    public long GetActiveCount() => Interlocked.Read(ref _activeCount);
}
