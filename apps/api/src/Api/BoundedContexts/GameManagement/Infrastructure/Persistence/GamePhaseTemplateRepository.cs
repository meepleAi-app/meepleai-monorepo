using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

internal sealed class GamePhaseTemplateRepository : IGamePhaseTemplateRepository
{
    private readonly MeepleAiDbContext _db;

    public GamePhaseTemplateRepository(MeepleAiDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<GamePhaseTemplate>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var entities = await _db.GamePhaseTemplates
            .Where(t => t.GameId == gameId)
            .OrderBy(t => t.PhaseOrder)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task AddRangeAsync(IEnumerable<GamePhaseTemplate> templates, CancellationToken ct = default)
    {
        var entities = templates.Select(MapToEntity).ToList();
        await _db.GamePhaseTemplates.AddRangeAsync(entities, ct).ConfigureAwait(false);
    }

    public async Task DeleteByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var entities = await _db.GamePhaseTemplates
            .Where(t => t.GameId == gameId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        _db.GamePhaseTemplates.RemoveRange(entities);
    }

    private static GamePhaseTemplate MapToDomain(GamePhaseTemplateEntity e)
        => GamePhaseTemplate.Restore(e.Id, e.GameId, e.PhaseName, e.PhaseOrder, e.CreatedBy, e.Description, e.CreatedAt, e.UpdatedAt);

    private static GamePhaseTemplateEntity MapToEntity(GamePhaseTemplate d) => new()
    {
        Id = d.Id,
        GameId = d.GameId,
        PhaseName = d.PhaseName,
        PhaseOrder = d.PhaseOrder,
        Description = d.Description,
        CreatedBy = d.CreatedBy,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };
}
