using Api.BoundedContexts.GameToolbox.Domain.Entities;

namespace Api.BoundedContexts.GameToolbox.Domain.Repositories;

/// <summary>
/// Repository interface for Toolbox templates.
/// </summary>
public interface IToolboxTemplateRepository
{
    Task<ToolboxTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ToolboxTemplate>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);
    Task<List<ToolboxTemplate>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(ToolboxTemplate toolboxTemplate, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
