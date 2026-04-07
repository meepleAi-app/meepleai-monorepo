using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Application.DTOs;

internal record AiServiceStatusDto(
    string Name,
    string DisplayName,
    string Type,
    ServiceHealthLevel Status,
    string Uptime,
    double AvgLatencyMs,
    double ErrorRate24h,
    DateTime LastCheckedAt,
    bool CanRestart,
    int? CooldownRemainingSeconds);

internal record AiServicesStatusResponse(IReadOnlyList<AiServiceStatusDto> Services);

internal record ServiceDependencyDto(
    string Name,
    string DisplayName,
    ServiceHealthLevel Status,
    double LatencyMs);

internal record ServiceDependenciesResponse(
    string ServiceName,
    IReadOnlyList<ServiceDependencyDto> Dependencies);

internal record PipelineHopDto(
    string ServiceName,
    string DisplayName,
    ServiceHealthLevel Status,
    double LatencyMs,
    string? Error);

internal record PipelineTestResponse(
    bool Success,
    IReadOnlyList<PipelineHopDto> Hops,
    double TotalLatencyMs);

internal record ServiceConfigParamDto(
    string Key,
    string DisplayName,
    string Value,
    string Type,
    string[]? Options,
    int? MinValue,
    int? MaxValue);

internal record ServiceConfigResponse(
    string ServiceName,
    IReadOnlyList<ServiceConfigParamDto> Parameters);

internal record RestartResponse(
    bool Success,
    string ServiceName,
    DateTime? CooldownExpiresAt,
    string? Message = null);

internal record HealthCheckResponse(
    string ServiceName,
    ServiceHealthLevel Status,
    string? Details,
    double LatencyMs);

internal record ConfigUpdateResponse(
    string ServiceName,
    IReadOnlyList<string> UpdatedParams);
