namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Provides monotonically increasing sequence numbers per session for SSE event IDs.
/// Issue #2561 SP2 T6: Replaces Ticks-based IDs with Redis INCR for monotonic ordering.
/// </summary>
public interface ISessionSequenceProvider
{
    /// <summary>
    /// Returns the next sequence number for the given session.
    /// Strictly increasing per session; independent counters across sessions.
    /// </summary>
    Task<long> NextAsync(Guid sessionId, CancellationToken ct);
}
