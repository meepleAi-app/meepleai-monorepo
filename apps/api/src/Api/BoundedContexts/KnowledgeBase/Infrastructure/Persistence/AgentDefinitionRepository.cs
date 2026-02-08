using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for AgentDefinition aggregate in KnowledgeBase bounded context.
/// Issue #3808 (Epic #3687)
/// </summary>
public sealed class AgentDefinitionRepository : IAgentDefinitionRepository
{
    private readonly MeepleAiDbContext _context;

    public AgentDefinitionRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AgentDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<AgentDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AgentDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Set<AgentDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<AgentDefinition>()
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentDefinition>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<AgentDefinition>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentDefinition>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync(cancellationToken).ConfigureAwait(false);

        var term = searchTerm.Trim().ToLowerInvariant();

        return await _context.Set<AgentDefinition>()
            .AsNoTracking()
            .Where(a => a.Name.ToLowerInvariant().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        a.Description.ToLowerInvariant().Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken = default)
    {
        await _context.Set<AgentDefinition>().AddAsync(agentDefinition, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken = default)
    {
        _context.Set<AgentDefinition>().Update(agentDefinition);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var agentDefinition = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (agentDefinition != null)
        {
            _context.Set<AgentDefinition>().Remove(agentDefinition);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Set<AgentDefinition>()
            .AsNoTracking()
            .AnyAsync(a => a.Name == name, cancellationToken).ConfigureAwait(false);
    }
}
