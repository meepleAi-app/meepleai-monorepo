namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// The live sessions whose result has already been counted against a memory (a player's or a group's), so that the
/// same session is never counted twice (#4839, option C).
/// </summary>
/// <remarks>
/// The older guard, <c>LastProcessedEventId</c>, remembers ONE event: it recognises a second delivery only when no
/// other event for the same player or group has been processed in between, which an outbox retry does not guarantee
/// (the batch commits what the handlers before the failing one saved, and the next event of the same player can run
/// before. A set keyed by the SESSION recognises it whatever came in between. It keeps the most recent
/// <see cref="Capacity"/> sessions, and that is a LIMIT, not a guarantee: the automatic retries of the outbox come
/// within minutes of the first delivery (ten attempts, a backoff of at most a minute), so a memory would have to count
/// that many sessions in that time to push one out; but a row that ended in <c>Failed</c> stays there until an admin
/// re-arms it (<c>RetryEventOutboxRow</c>), which can be days later. If the memory has counted <see cref="Capacity"/>
/// other sessions in the meantime, the session is no longer remembered and would be counted again. The memory of the
/// anonymous guest is shared by every session of every user that names a guest the same way, so it fills fastest.
/// A table keyed by <c>(memory, session)</c> would have no limit; it needs a unique index and a way to forget what was
/// staged when two deliveries race for it (#4753), which is why this is a bounded set in the row of the memory.
/// </remarks>
internal sealed class ProcessedSessions
{
    /// <summary>How many sessions are remembered per memory.</summary>
    public const int Capacity = 200;

    private readonly List<Guid> _ids;

    public ProcessedSessions(IEnumerable<Guid>? ids = null)
    {
        _ids = ids?.Where(id => id != Guid.Empty).Distinct().TakeLast(Capacity).ToList() ?? [];
    }

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<Guid> Ids => _ids.AsReadOnly();

    public bool Contains(Guid sessionId) => _ids.Contains(sessionId);

    /// <summary>Remembers the session; a session already remembered is left where it is.</summary>
    public void Add(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        if (_ids.Contains(sessionId))
            return;

        _ids.Add(sessionId);
        if (_ids.Count > Capacity)
            _ids.RemoveAt(0);
    }
}
