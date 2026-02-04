using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of AgentGameStateSnapshot repository.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal class AgentGameStateSnapshotRepository : RepositoryBase, IAgentGameStateSnapshotRepository
{
    public AgentGameStateSnapshotRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AgentGameStateSnapshot?> GetLatestByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentGameStateSnapshotEntity>()
            .AsNoTracking()
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.TurnNumber)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<AgentGameStateSnapshot>> GetByGameIdAsync(
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentGameStateSnapshotEntity>()
            .AsNoTracking()
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.TurnNumber)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentGameStateSnapshotEntity>()
            .CountAsync(s => s.GameId == gameId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentGameStateSnapshot?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentGameStateSnapshotEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(AgentGameStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(snapshot);
        await DbContext.Set<AgentGameStateSnapshotEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(AgentGameStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(snapshot);

        // Detach existing tracked entity to avoid conflicts
        var tracked = DbContext.ChangeTracker.Entries<AgentGameStateSnapshotEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.Set<AgentGameStateSnapshotEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentGameStateSnapshotEntity>()
            .Where(s => s.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    // ========== Mapping Methods ==========

    private static AgentGameStateSnapshot MapToDomain(AgentGameStateSnapshotEntity entity)
    {
        Vector? embedding = null;
        if (entity.Embedding != null)
        {
            // Issue #3547: Convert Pgvector.Vector → float[] → Domain.Vector
            embedding = new Vector(entity.Embedding.ToArray());
        }

        var snapshot = new AgentGameStateSnapshot(
            id: entity.Id,
            gameId: entity.GameId,
            agentSessionId: entity.AgentSessionId,
            boardStateJson: entity.BoardStateJson,
            turnNumber: entity.TurnNumber,
            activePlayerId: entity.ActivePlayerId,
            embedding: embedding);

        // Override the auto-generated timestamp with the persisted value
        var createdAtProp = typeof(AgentGameStateSnapshot).GetProperty("CreatedAt");
        createdAtProp?.SetValue(snapshot, entity.CreatedAt);

        return snapshot;
    }

    private static AgentGameStateSnapshotEntity MapToEntity(AgentGameStateSnapshot snapshot)
    {
        return new AgentGameStateSnapshotEntity
        {
            Id = snapshot.Id,
            GameId = snapshot.GameId,
            AgentSessionId = snapshot.AgentSessionId,
            BoardStateJson = snapshot.BoardStateJson,
            TurnNumber = snapshot.TurnNumber,
            ActivePlayerId = snapshot.ActivePlayerId,
            CreatedAt = snapshot.CreatedAt,
            // Issue #3547: Convert Domain.Vector → float[] → Pgvector.Vector
            Embedding = snapshot.Embedding != null ? new Pgvector.Vector(snapshot.Embedding.Values) : null
        };
    }
}
