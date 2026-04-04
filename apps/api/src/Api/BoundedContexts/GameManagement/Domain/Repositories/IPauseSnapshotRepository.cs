using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for PauseSnapshot persistence.
/// PauseSnapshot stores full-state snapshots captured at session pause time,
/// separate from delta-based SessionSnapshot.
/// </summary>
public interface IPauseSnapshotRepository
{
    Task<PauseSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent snapshot for a session, ordered by SavedAt descending.
    /// Returns null if the session has no snapshots.
    /// </summary>
    Task<PauseSnapshot?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Returns all snapshots for a session ordered by SavedAt ascending.
    /// </summary>
    Task<IReadOnlyList<PauseSnapshot>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);

    Task AddAsync(PauseSnapshot snapshot, CancellationToken ct = default);

    Task UpdateAsync(PauseSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Deletes all auto-save snapshots for the given session.
    /// Used on resume to discard stale auto-saves and keep history clean.
    /// </summary>
    Task DeleteAutoSavesBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
}
