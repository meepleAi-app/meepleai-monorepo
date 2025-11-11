using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for ChatThread aggregate.
/// </summary>
public interface IChatThreadRepository : IRepository<ChatThread, Guid>
{
    /// <summary>
    /// Finds threads by game ID.
    /// </summary>
    Task<IReadOnlyList<ChatThread>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds recent threads (ordered by last message).
    /// </summary>
    Task<IReadOnlyList<ChatThread>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default);
}
