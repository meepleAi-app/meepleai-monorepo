using Api.BoundedContexts.Administration.Domain.Models;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Issue #894: Domain service for aggregated infrastructure details.
/// Orchestrates health checks and Prometheus metrics in a single view.
/// </summary>
public interface IInfrastructureDetailsService
{
    /// <summary>
    /// Gets comprehensive infrastructure details including health status and metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated infrastructure details</returns>
    Task<InfrastructureDetails> GetDetailsAsync(CancellationToken cancellationToken = default);
}
