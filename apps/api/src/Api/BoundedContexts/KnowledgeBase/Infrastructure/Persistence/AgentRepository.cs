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
public class AgentRepository : RepositoryBase, IAgentRepository
{
    public AgentRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<Agent?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var entity = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == name, ct).ConfigureAwait(false);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<List<Agent>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetByTypeAsync(AgentType type, CancellationToken ct = default)
    {
        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.Type == type.Value)
            .OrderBy(a => a.Name)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetIdleAgentsAsync(CancellationToken ct = default)
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        var entities = await DbContext.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.LastInvokedAt == null || a.LastInvokedAt < sevenDaysAgo)
            .OrderBy(a => a.LastInvokedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task AddAsync(Agent agent, CancellationToken ct = default)
    {
        CollectDomainEvents(agent);
        var entity = KnowledgeBaseMappers.ToEntity(agent);
        await DbContext.Set<AgentEntity>().AddAsync(entity, ct).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Agent agent, CancellationToken ct = default)
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
        await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.Set<AgentEntity>()
            .FirstOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false);

        if (entity != null)
        {
            DbContext.Set<AgentEntity>().Remove(entity);
            await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken ct = default)
    {
        return await DbContext.Set<AgentEntity>()
            .AnyAsync(a => a.Name == name, ct).ConfigureAwait(false);
    }
}
