namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Response DTO for service metrics and performance data.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public record ServiceMetricsDto(
    string ServiceName,
    string UptimePercentage,
    string AverageLatency,
    long RequestCount,
    DateTime LastChecked
);

/// <summary>
/// Response DTO for aggregated service metrics query.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public record ServiceMetricsResponseDto(
    IReadOnlyList<ServiceMetricsDto> Services,
    DateTime Timestamp
);
