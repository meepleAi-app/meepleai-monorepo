using Api.BoundedContexts.Administration.Domain.Services;

namespace Api.BoundedContexts.Administration.Domain.Models;

/// <summary>
/// Issue #894: Aggregated infrastructure details combining health and metrics.
/// </summary>
/// <param name="Overall">Overall infrastructure health status</param>
/// <param name="Services">Individual service health statuses</param>
/// <param name="Metrics">Prometheus metrics summary</param>
public record InfrastructureDetails(
    OverallHealthStatus Overall,
    IReadOnlyCollection<ServiceHealthStatus> Services,
    PrometheusMetricsSummary Metrics
);
