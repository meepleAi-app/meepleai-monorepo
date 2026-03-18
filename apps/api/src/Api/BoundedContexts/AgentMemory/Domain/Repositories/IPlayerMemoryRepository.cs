using Api.BoundedContexts.AgentMemory.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Repositories;

/// <summary>
/// Repository interface for PlayerMemory aggregate persistence.
/// </summary>
internal interface IPlayerMemoryRepository
{
    Task<PlayerMemory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PlayerMemory?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerMemory>> GetByGroupIdAsync(Guid groupId, CancellationToken ct = default);
    Task<PlayerMemory?> GetByGuestNameAsync(string guestName, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerMemory>> GetAllByGuestNameAsync(string guestName, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerMemory>> GetAllByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(PlayerMemory memory, CancellationToken ct = default);
    Task UpdateAsync(PlayerMemory memory, CancellationToken ct = default);
}
