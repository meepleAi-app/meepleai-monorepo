using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for SessionMedia persistence operations.
/// Issue #4760 - SessionMedia Entity + RAG Agent Integration + Shared Chat
/// </summary>
public interface ISessionMediaRepository
{
    Task<SessionMedia?> GetByIdAsync(Guid mediaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionMedia>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionMedia>> GetBySnapshotIdAsync(Guid snapshotId, CancellationToken cancellationToken = default);

    Task<int> GetCountBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task AddAsync(SessionMedia media, CancellationToken cancellationToken = default);

    Task UpdateAsync(SessionMedia media, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
