namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// The live sessions whose result has already been counted against a memory (a player's or a group's), so that the
/// same session is never counted twice (#4839, option C).
/// </summary>
/// <remarks>
/// The older guard, <c>LastProcessedEventId</c>, remembers ONE event: it recognises a second delivery only when no
/// other event for the same player or group has been processed in between, which an outbox retry does not guarantee
/// (the batch commits what the handlers before the failing one saved, and the next event of the same player can run
/// before the retry). A set keyed by the SESSION recognises it whatever came in between. It keeps the most recent
/// <see cref="Capacity"/> sessions: a redelivery comes within minutes of the first delivery (ten attempts with a
/// backoff of at most a minute), so a player would have to complete that many sessions in that time to push one out.
/// </remarks>
internal sealed class ProcessedSessions
{
    /// <summary>How many sessions are remembered per memory.</summary>
    public const int Capacity = 50;

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
