using Api.BoundedContexts.AgentMemory.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Repositories;

/// <summary>
/// Repository interface for GroupMemory aggregate persistence.
/// </summary>
internal interface IGroupMemoryRepository
{
    Task<GroupMemory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GroupMemory>> GetByCreatorIdAsync(Guid creatorId, CancellationToken ct = default);

    /// <summary>
    /// Returns the ids of the groups the given user is a member of (the inverse of
    /// <see cref="GetByCreatorIdAsync"/>). Used by the game leaderboard (#1467) to resolve
    /// the set of Group-visible play records the user is allowed to see.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetGroupIdsForUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(GroupMemory memory, CancellationToken ct = default);
    Task UpdateAsync(GroupMemory memory, CancellationToken ct = default);
}
