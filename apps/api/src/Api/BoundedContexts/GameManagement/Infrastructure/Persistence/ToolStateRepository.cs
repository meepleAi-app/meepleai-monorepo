using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ToolState repository.
/// Maps between domain ToolState entity and ToolStateEntity persistence model.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
internal class ToolStateRepository : IToolStateRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public ToolStateRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ToolState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ToolStates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<ToolState>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.ToolStates
            .AsNoTracking()
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.ToolName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ToolState?> GetBySessionAndToolNameAsync(Guid sessionId, string toolName, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ToolStates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SessionId == sessionId && t.ToolName == toolName, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(ToolState toolState, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(toolState);
        await _dbContext.ToolStates.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddRangeAsync(IEnumerable<ToolState> toolStates, CancellationToken cancellationToken = default)
    {
        var entities = toolStates.Select(MapToPersistence).ToList();
        await _dbContext.ToolStates.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ToolState toolState, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(toolState);

        // Detach existing tracked entity to avoid conflicts
        var tracked = _dbContext.ChangeTracker.Entries<ToolStateEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        _dbContext.ToolStates.Update(entity);
        return Task.CompletedTask;
    }

    private static ToolState MapToDomain(ToolStateEntity entity)
    {
        // Use the domain constructor directly - ToolState is a simple Entity<Guid>, not an aggregate
        return new ToolState(
            id: entity.Id,
            sessionId: entity.SessionId,
            toolkitId: entity.ToolkitId,
            toolName: entity.ToolName,
            toolType: (ToolType)entity.ToolType,
            initialStateJson: entity.StateDataJson);
    }

    private static ToolStateEntity MapToPersistence(ToolState toolState)
    {
        return new ToolStateEntity
        {
            Id = toolState.Id,
            SessionId = toolState.SessionId,
            ToolkitId = toolState.ToolkitId,
            ToolName = toolState.ToolName,
            ToolType = (int)toolState.ToolType,
            StateDataJson = toolState.StateDataJson,
            CreatedAt = toolState.CreatedAt,
            LastUpdatedAt = toolState.LastUpdatedAt
        };
    }
}
