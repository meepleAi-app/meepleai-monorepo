using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for ChatThread aggregate.
/// </summary>
internal interface IChatThreadRepository : IRepository<ChatThread, Guid>
{
    /// <summary>
    /// Finds threads by user ID.
    /// </summary>
    Task<IReadOnlyList<ChatThread>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds threads by game ID.
    /// </summary>
    Task<IReadOnlyList<ChatThread>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds threads by user ID and game ID.
    /// </summary>
    Task<IReadOnlyList<ChatThread>> FindByUserIdAndGameIdAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds recent threads (ordered by last message).
    /// </summary>
    /// <summary>
    /// Counts total threads for a user (for pagination).
    /// </summary>
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatThread>> GetRecentAsync(int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds threads for a user with filters and pagination (Issue #4362).
    /// </summary>
    Task<(IReadOnlyList<ChatThread> Items, int TotalCount)> FindByUserIdFilteredAsync(
        Guid userId,
        Guid? gameId = null,
        string? agentType = null,
        string? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
