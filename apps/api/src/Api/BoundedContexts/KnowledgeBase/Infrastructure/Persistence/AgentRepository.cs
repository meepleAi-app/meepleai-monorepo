using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for Agent aggregate in KnowledgeBase bounded context.
/// </summary>
public class AgentRepository : IAgentRepository
{
    private readonly MeepleAiDbContext _context;

    public AgentRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.Set<AgentEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<Agent?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var entity = await _context.Set<AgentEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == name, ct);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<List<Agent>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _context.Set<AgentEntity>()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var entities = await _context.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetByTypeAsync(AgentType type, CancellationToken ct = default)
    {
        var entities = await _context.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.Type == type.Value)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<Agent>> GetIdleAgentsAsync(CancellationToken ct = default)
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        var entities = await _context.Set<AgentEntity>()
            .AsNoTracking()
            .Where(a => a.LastInvokedAt == null || a.LastInvokedAt < sevenDaysAgo)
            .OrderBy(a => a.LastInvokedAt)
            .ToListAsync(ct);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task AddAsync(Agent agent, CancellationToken ct = default)
    {
        var entity = KnowledgeBaseMappers.ToEntity(agent);
        await _context.Set<AgentEntity>().AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Agent agent, CancellationToken ct = default)
    {
        var entity = KnowledgeBaseMappers.ToEntity(agent);

        // Detach any existing tracked entity to prevent tracking conflicts
        var trackedEntity = _context.ChangeTracker.Entries<AgentEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);
        if (trackedEntity != null)
        {
            trackedEntity.State = EntityState.Detached;
        }

        _context.Set<AgentEntity>().Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.Set<AgentEntity>()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (entity != null)
        {
            _context.Set<AgentEntity>().Remove(entity);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken ct = default)
    {
        return await _context.Set<AgentEntity>()
            .AnyAsync(a => a.Name == name, ct);
    }
}
