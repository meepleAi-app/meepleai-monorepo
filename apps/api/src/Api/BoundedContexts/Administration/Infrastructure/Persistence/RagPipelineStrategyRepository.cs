using Api.BoundedContexts.Administration.Domain.Aggregates.RagPipelineStrategy;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for RagPipelineStrategy aggregate.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
internal sealed class RagPipelineStrategyRepository : RepositoryBase, IRagPipelineStrategyRepository
{

    public RagPipelineStrategyRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<RagPipelineStrategy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<RagPipelineStrategy>()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RagPipelineStrategy>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<RagPipelineStrategy>()
            .Where(s => s.CreatedByUserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RagPipelineStrategy>> GetTemplatesAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<RagPipelineStrategy>()
            .Where(s => s.IsTemplate);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.TemplateCategory == category);
        }

        return await query
            .OrderBy(s => s.TemplateCategory)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RagPipelineStrategy>> SearchAsync(
        string? searchTerm,
        Guid? userId = null,
        bool includeTemplates = true,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<RagPipelineStrategy>().AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(s => s.CreatedByUserId == userId.Value || s.IsTemplate);
        }

        if (!includeTemplates)
        {
            query = query.Where(s => !s.IsTemplate);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToUpperInvariant();
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, $"%{term}%") ||
                EF.Functions.ILike(s.Description, $"%{term}%"));
        }

        return await query
            .OrderByDescending(s => s.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RagPipelineStrategy> AddAsync(RagPipelineStrategy strategy, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<RagPipelineStrategy>()
            .AddAsync(strategy, cancellationToken)
            .ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return strategy;
    }

    public async Task UpdateAsync(RagPipelineStrategy strategy, CancellationToken cancellationToken = default)
    {
        DbContext.Set<RagPipelineStrategy>().Update(strategy);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(RagPipelineStrategy strategy, CancellationToken cancellationToken = default)
    {
        strategy.Delete();
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid userId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<RagPipelineStrategy>()
            .Where(s => s.Name == name && s.CreatedByUserId == userId);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }
}
