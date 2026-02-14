using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for ResourceForecast aggregate.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal class ResourceForecastRepository : RepositoryBase, IResourceForecastRepository
{
    public ResourceForecastRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ResourceForecast?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.ResourceForecasts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResourceForecast>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.ResourceForecasts
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(ResourceForecast entity, CancellationToken cancellationToken = default)
    {
        await DbContext.ResourceForecasts.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ResourceForecast entity, CancellationToken cancellationToken = default)
    {
        DbContext.ResourceForecasts.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(ResourceForecast entity, CancellationToken cancellationToken = default)
    {
        DbContext.ResourceForecasts.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.ResourceForecasts
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ResourceForecast> Forecasts, int Total)> GetByUserAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.ResourceForecasts
            .AsNoTracking()
            .Where(e => e.CreatedByUserId == userId)
            .OrderByDescending(e => e.CreatedAt);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var forecasts = await query
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (forecasts.AsReadOnly(), total);
    }
}
