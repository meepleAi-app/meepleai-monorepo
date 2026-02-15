using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for PlaygroundTestScenario aggregate.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
public sealed class PlaygroundTestScenarioRepository : IPlaygroundTestScenarioRepository
{
    private readonly MeepleAiDbContext _context;

    public PlaygroundTestScenarioRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PlaygroundTestScenario?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<PlaygroundTestScenario>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<List<PlaygroundTestScenario>> GetAllAsync(
        ScenarioCategory? category = null,
        Guid? agentDefinitionId = null,
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        var query = _context.Set<PlaygroundTestScenario>().AsNoTracking();

        if (activeOnly)
            query = query.Where(s => s.IsActive);

        if (category.HasValue)
            query = query.Where(s => s.Category == category.Value);

        if (agentDefinitionId.HasValue)
            query = query.Where(s => s.AgentDefinitionId == agentDefinitionId.Value);

        return await query
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(PlaygroundTestScenario scenario, CancellationToken ct = default)
    {
        await _context.Set<PlaygroundTestScenario>().AddAsync(scenario, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(PlaygroundTestScenario scenario, CancellationToken ct = default)
    {
        _context.Set<PlaygroundTestScenario>().Update(scenario);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
