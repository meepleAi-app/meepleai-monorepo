using Api.BoundedContexts.GameToolbox.Domain.Entities;

namespace Api.BoundedContexts.GameToolbox.Domain.Repositories;

/// <summary>
/// Repository interface for the Toolbox aggregate.
/// </summary>
public interface IToolboxRepository
{
    Task<Toolbox?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Toolbox?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);
    Task AddAsync(Toolbox toolbox, CancellationToken ct = default);
    Task UpdateAsync(Toolbox toolbox, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
