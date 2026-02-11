using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for CostScenario aggregate.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal class CostScenarioRepository : RepositoryBase, ICostScenarioRepository
{
    public CostScenarioRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<CostScenario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.CostScenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CostScenario>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.CostScenarios
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(CostScenario entity, CancellationToken cancellationToken = default)
    {
        await DbContext.CostScenarios.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(CostScenario entity, CancellationToken cancellationToken = default)
    {
        DbContext.CostScenarios.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(CostScenario entity, CancellationToken cancellationToken = default)
    {
        DbContext.CostScenarios.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.CostScenarios
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<CostScenario> Scenarios, int Total)> GetByUserAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.CostScenarios
            .AsNoTracking()
            .Where(e => e.CreatedByUserId == userId)
            .OrderByDescending(e => e.CreatedAt);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var scenarios = await query
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (scenarios.AsReadOnly(), total);
    }
}
