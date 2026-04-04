using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

internal interface IGamePhaseTemplateRepository
{
    Task<IReadOnlyList<GamePhaseTemplate>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<GamePhaseTemplate> templates, CancellationToken ct = default);
    Task DeleteByGameIdAsync(Guid gameId, CancellationToken ct = default);
}
