using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Repositories;

/// <summary>
/// Repository interface for CostScenario aggregate.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal interface ICostScenarioRepository : IRepository<CostScenario, Guid>
{
    /// <summary>
    /// Gets scenarios for a specific user, ordered by creation date descending.
    /// </summary>
    Task<(IReadOnlyList<CostScenario> Scenarios, int Total)> GetByUserAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
