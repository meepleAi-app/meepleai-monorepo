using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;

/// <summary>
/// Repository interface for N8nConfiguration aggregate.
/// </summary>
public interface IN8nConfigurationRepository : IRepository<N8nConfiguration, Guid>
{
    /// <summary>
    /// Gets active configuration.
    /// </summary>
    Task<N8nConfiguration?> GetActiveConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds configuration by name.
    /// </summary>
    Task<N8nConfiguration?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
