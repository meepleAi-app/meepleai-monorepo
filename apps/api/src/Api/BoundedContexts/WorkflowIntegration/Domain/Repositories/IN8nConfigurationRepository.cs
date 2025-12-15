using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;

/// <summary>
/// Repository interface for N8NConfiguration aggregate.
/// </summary>
internal interface IN8NConfigurationRepository : IRepository<N8NConfiguration, Guid>
{
    /// <summary>
    /// Gets active configuration.
    /// </summary>
    Task<N8NConfiguration?> GetActiveConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds configuration by name.
    /// </summary>
    Task<N8NConfiguration?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
