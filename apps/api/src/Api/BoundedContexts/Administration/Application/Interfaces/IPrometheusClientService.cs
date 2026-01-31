namespace Api.BoundedContexts.Administration.Application.Interfaces;

/// <summary>
/// Service interface for querying Prometheus metrics API (Issue #2139)
/// </summary>
internal interface IPrometheusClientService
{
    /// <summary>
    /// Executes a PromQL query against Prometheus
    /// </summary>
    /// <param name="query">PromQL query string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Query result as dynamic object or null if query fails</returns>
    Task<object?> QueryAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if Prometheus is available and responsive
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
