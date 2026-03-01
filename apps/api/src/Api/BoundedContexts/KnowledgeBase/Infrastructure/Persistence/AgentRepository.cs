using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for Agent aggregate in KnowledgeBase bounded context.
/// </summary>
internal class AgentRepository : RepositoryBase, IAgentRepository
{
    public AgentRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<Agent?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<List<Agent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetByTypeAsync(AgentType type, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.Type == type.Value)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetIdleAgentsAsync(CancellationToken cancellationToken = default)
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.LastInvokedAt == null || a.LastInvokedAt < sevenDaysAgo)
            .OrderBy(a => a.LastInvokedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task AddAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(agent);
        var entity = KnowledgeBaseMappers.ToEntity(agent);
        await DbContext.Set<AgentEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(agent);
        var entity = KnowledgeBaseMappers.ToEntity(agent);

        // Detach any existing tracked entity to prevent tracking conflicts
        var trackedEntity = DbContext.ChangeTracker.Entries<AgentEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);
        if (trackedEntity != null)
        {
            trackedEntity.State = EntityState.Detached;
        }

        DbContext.Set<AgentEntity>().Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentEntity>()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

        if (entity != null)
        {
            DbContext.Set<AgentEntity>().Remove(entity);
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentEntity>()
            .AnyAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Agent>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.GameId == gameId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.CreatedByUserId == userId && a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<bool> ExistsByNameForUserAsync(Guid userId, string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentEntity>()
            .AnyAsync(a => a.CreatedByUserId == userId && a.Name == name && a.IsActive, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Guid?> ResolveGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        // Single query: match games.Id directly OR resolve via games.SharedGameId
        return await DbContext.Games
            .AsNoTracking()
            .Where(g => g.Id == gameId || g.SharedGameId == gameId)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}

