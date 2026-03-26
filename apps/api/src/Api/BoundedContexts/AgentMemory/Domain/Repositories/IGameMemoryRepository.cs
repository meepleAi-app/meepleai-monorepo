using Api.BoundedContexts.AgentMemory.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Repositories;

/// <summary>
/// Repository interface for GameMemory aggregate persistence.
/// </summary>
internal interface IGameMemoryRepository
{
    Task<GameMemory?> GetByGameAndOwnerAsync(Guid gameId, Guid ownerId, CancellationToken ct = default);
    Task<GameMemory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(GameMemory memory, CancellationToken ct = default);
    Task UpdateAsync(GameMemory memory, CancellationToken ct = default);
}
