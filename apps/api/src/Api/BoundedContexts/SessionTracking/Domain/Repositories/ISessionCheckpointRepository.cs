using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for SessionCheckpoint aggregate.
/// Issue #278 - Session Checkpoint / Deep Save
/// </summary>
public interface ISessionCheckpointRepository
{
    Task<SessionCheckpoint?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<SessionCheckpoint>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task AddAsync(SessionCheckpoint checkpoint, CancellationToken ct = default);
    Task UpdateAsync(SessionCheckpoint checkpoint, CancellationToken ct = default);
}
