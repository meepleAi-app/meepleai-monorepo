using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for live game session aggregate.
/// In-memory backed since LiveGameSession is not EF-persisted.
/// Issue #4749: CQRS commands/queries for live sessions.
/// </summary>
internal interface ILiveSessionRepository
{
    Task<LiveGameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LiveGameSession?> GetByCodeAsync(string sessionCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LiveGameSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(LiveGameSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(LiveGameSession session, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
