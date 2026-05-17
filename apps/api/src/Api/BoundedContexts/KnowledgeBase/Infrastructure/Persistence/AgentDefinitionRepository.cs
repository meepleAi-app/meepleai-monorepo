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

        var pattern = $"%{searchTerm.Trim()}%";

        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .Where(a => EF.Functions.ILike(a.Name, pattern) ||
                        EF.Functions.ILike(a.Description, pattern))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken = default)
    {
        // ADR-056: repository methods mutate the change-tracker only.
        // Callers are responsible for invoking IUnitOfWork.SaveChangesAsync(ct).
        await DbContext.Set<AgentDefinition>().AddAsync(agentDefinition, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken = default)
    {
        // Avoid IdentityConflict: another entity with same Id may already be tracked from prior reads
        // (test seeding, EF caching). Detach it before re-attaching the modified version.
        var tracked = DbContext.ChangeTracker.Entries<AgentDefinition>()
            .FirstOrDefault(e => e.Entity.Id == agentDefinition.Id);
        if (tracked != null && !ReferenceEquals(tracked.Entity, agentDefinition))
        {
            tracked.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
        DbContext.Set<AgentDefinition>().Update(agentDefinition);
        // ADR-056: caller persists via IUnitOfWork.SaveChangesAsync.
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var agentDefinition = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (agentDefinition != null)
        {
            DbContext.Set<AgentDefinition>().Remove(agentDefinition);
            // ADR-056: caller persists via IUnitOfWork.SaveChangesAsync.
        }
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .AnyAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AgentDefinition?> GetByIdIgnoreDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters bypasses the global HasQueryFilter(a => !a.IsDeleted) filter,
        // allowing restore operations to fetch soft-deleted aggregates.
        return await DbContext.Set<AgentDefinition>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> CountActiveByGameIdsAsync(IReadOnlyList<Guid> gameIds, CancellationToken cancellationToken = default)
    {
        if (gameIds is null || gameIds.Count == 0)
            return 0;

        // Count agents linked to any of the provided gameIds.
        // The global HasQueryFilter ensures soft-deleted agents are excluded automatically.
        return await DbContext.Set<AgentDefinition>()
            .AsNoTracking()
            .CountAsync(a => a.GameId != null && gameIds.Contains(a.GameId.Value), cancellationToken)
            .ConfigureAwait(false);
    }
}
