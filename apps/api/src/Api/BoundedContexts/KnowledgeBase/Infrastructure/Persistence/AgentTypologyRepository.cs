using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for AgentTypology aggregate in KnowledgeBase bounded context.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
internal class AgentTypologyRepository : RepositoryBase, IAgentTypologyRepository
{
    public AgentTypologyRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AgentTypology?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentTypologyEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<AgentTypology?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentTypologyEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);

        return entity != null ? KnowledgeBaseMappers.ToDomain(entity) : null;
    }

    public async Task<List<AgentTypology>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentTypologyEntity>()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task<List<AgentTypology>> GetApprovedAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentTypologyEntity>()
            .AsNoTracking()
            .Where(a => a.Status == 2 && !a.IsDeleted) // Status 2 = Approved
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(KnowledgeBaseMappers.ToDomain).ToList();
    }

    public async Task AddAsync(AgentTypology typology, CancellationToken cancellationToken = default)
    {
        var entity = KnowledgeBaseMappers.ToEntity(typology);
        await DbContext.Set<AgentTypologyEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
        CollectDomainEvents(typology);
    }

    public async Task UpdateAsync(AgentTypology typology, CancellationToken cancellationToken = default)
    {
        var entity = KnowledgeBaseMappers.ToEntity(typology);
        DbContext.Set<AgentTypologyEntity>().Update(entity);
        CollectDomainEvents(typology);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentTypologyEntity>()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

        if (entity != null)
        {
            DbContext.Set<AgentTypologyEntity>().Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentTypologyEntity>()
            .AsNoTracking()
            .AnyAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string name, Guid excludeId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentTypologyEntity>()
            .AsNoTracking()
            .AnyAsync(a => a.Name == name && a.Id != excludeId, cancellationToken).ConfigureAwait(false);
    }
}
