using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for AgentDefinition aggregate in KnowledgeBase bounded context.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed class AgentDefinitionRepository : RepositoryBase, IAgentDefinitionRepository
{

    public AgentDefinitionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AgentDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentDefinition>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentDefinition>> GetAllPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .Where(a => a.Status == AgentDefinitionStatus.Published && a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentDefinition>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync(cancellationToken).ConfigureAwait(false);

        var term = searchTerm.Trim().ToLowerInvariant();

        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .Where(a => a.Name.ToLowerInvariant().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        a.Description.ToLowerInvariant().Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<AgentDefinition>().AddAsync(agentDefinition, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken = default)
    {
        DbContext.Set<AgentDefinition>().Update(agentDefinition);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var agentDefinition = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (agentDefinition != null)
        {
            DbContext.Set<AgentDefinition>().Remove(agentDefinition);
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .AnyAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);
    }
}
