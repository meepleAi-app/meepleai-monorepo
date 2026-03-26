using Api.BoundedContexts.AgentMemory.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Repositories;

/// <summary>
/// Repository interface for GroupMemory aggregate persistence.
/// </summary>
internal interface IGroupMemoryRepository
{
    Task<GroupMemory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GroupMemory>> GetByCreatorIdAsync(Guid creatorId, CancellationToken ct = default);
    Task AddAsync(GroupMemory memory, CancellationToken ct = default);
    Task UpdateAsync(GroupMemory memory, CancellationToken ct = default);
}
