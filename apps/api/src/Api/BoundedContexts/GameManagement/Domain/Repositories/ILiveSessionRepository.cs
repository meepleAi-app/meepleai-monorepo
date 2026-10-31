using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository for the LiveGameSession aggregate, EF Core-backed.
/// Issue #2097 / ADR-060: Replaced in-memory ConcurrentDictionary with persistent
/// storage. Live sessions survive container restarts and scale multi-instance.
/// </summary>
internal interface ILiveSessionRepository
{
    Task<LiveGameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LiveGameSession?> GetByCodeAsync(string sessionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// The live session whose <see cref="LiveGameSession.TrackingSessionId"/> is
    /// <paramref name="trackingSessionId"/> (#4752: a game night links to the tracking Session, and
    /// the live session is reached from it), or null. If more than one carries the id — which the
    /// bridge never creates — the oldest wins, so the answer is stable.
    /// </summary>
    Task<LiveGameSession?> GetByTrackingSessionIdAsync(Guid trackingSessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LiveGameSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// #3146 / Invariante 4: projection-only lookup of the user's most-recent genuinely-LIVE
    /// (InProgress) session id, or null. Avoids materializing the full aggregate graph on the
    /// play-record save path where only the id is needed for the non-blocking warning deep-link.
    /// </summary>
    Task<Guid?> GetActiveInProgressSessionIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Gets all active (in-progress) sessions across all users. Used by the auto-save background service.</summary>
    Task<IReadOnlyList<LiveGameSession>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task AddAsync(LiveGameSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(LiveGameSession session, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
