using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Repositories;

/// <summary>
/// Repository interface for ResourceForecast aggregate.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal interface IResourceForecastRepository : IRepository<ResourceForecast, Guid>
{
    /// <summary>
    /// Gets forecasts for a specific user, ordered by creation date descending.
    /// </summary>
    Task<(IReadOnlyList<ResourceForecast> Forecasts, int Total)> GetByUserAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
