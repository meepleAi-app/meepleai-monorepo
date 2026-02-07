namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Response DTO for service health status.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public record ServiceHealthDto(
    string ServiceName,
    string Status,
    string? Description,
    bool IsCritical,
    DateTime Timestamp
);

/// <summary>
/// Response DTO for aggregated service health query.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public record ServiceHealthResponseDto(
    string OverallStatus,
    IReadOnlyList<ServiceHealthDto> Services,
    DateTime Timestamp
);
