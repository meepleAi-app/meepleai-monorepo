namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Enhanced service dashboard response with uptime, trends, and categories.
/// Issue #132: Enhanced ServiceHealthMatrix.
/// </summary>
public sealed record EnhancedServiceDashboardDto(
    OverallHealthStatusDto Overall,
    IReadOnlyList<EnhancedServiceHealthDto> Services,
    PrometheusMetricsDto PrometheusMetrics
);

/// <summary>
/// Overall health status summary.
/// </summary>
public sealed record OverallHealthStatusDto(
    string State,
    int TotalServices,
    int HealthyServices,
    int DegradedServices,
    int UnhealthyServices,
    DateTime CheckedAt
);

/// <summary>
/// Enhanced per-service health with category, uptime, and trend data.
/// </summary>
public sealed record EnhancedServiceHealthDto(
    string ServiceName,
    string State,
    string? ErrorMessage,
    DateTime CheckedAt,
    double ResponseTimeMs,
    string Category,
    double UptimePercent24h,
    string ResponseTimeTrend,
    double? PreviousResponseTimeMs,
    DateTime? LastIncidentAt
);

/// <summary>
/// Prometheus metrics summary DTO.
/// </summary>
public sealed record PrometheusMetricsDto(
    long ApiRequestsLast24h,
    double AvgLatencyMs,
    double ErrorRate,
    double LlmCostLast24h
);
