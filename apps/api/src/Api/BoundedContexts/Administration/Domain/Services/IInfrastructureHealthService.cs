

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Domain service for infrastructure health monitoring.
/// Issue #891: Aggregates health checks from all infrastructure services.
/// </summary>
internal interface IInfrastructureHealthService
{
    /// <summary>
    /// Gets the health status of a specific service.
    /// </summary>
    /// <param name="serviceName">Name of the service (postgres, redis, qdrant, n8n, prometheus)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service health status</returns>
    Task<ServiceHealthStatus> GetServiceHealthAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the health status of all monitored services.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of all service health statuses</returns>
    Task<IReadOnlyCollection<ServiceHealthStatus>> GetAllServicesHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the aggregated overall health status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Overall infrastructure health status</returns>
    Task<OverallHealthStatus> GetOverallHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the health status of a single service.
/// </summary>
internal record ServiceHealthStatus(
    string ServiceName,
    HealthState State,
    string? ErrorMessage,
    DateTime CheckedAt,
    TimeSpan ResponseTime);

/// <summary>
/// Represents the overall health status of all infrastructure.
/// </summary>
internal record OverallHealthStatus(
    HealthState State,
    int TotalServices,
    int HealthyServices,
    int DegradedServices,
    int UnhealthyServices,
    DateTime CheckedAt);

/// <summary>
/// Health state enumeration.
/// </summary>
internal enum HealthState
{
    /// <summary>Service is healthy and responding</summary>
    Healthy = 0,

    /// <summary>Service is partially functional (degraded performance)</summary>
    Degraded = 1,

    /// <summary>Service is unhealthy or not responding</summary>
    Unhealthy = 2
}
