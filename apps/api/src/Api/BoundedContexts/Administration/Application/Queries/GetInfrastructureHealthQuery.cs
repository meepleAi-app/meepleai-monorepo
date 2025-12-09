using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get infrastructure health status.
/// Issue #891: Infrastructure monitoring service.
/// </summary>
public record GetInfrastructureHealthQuery : IRequest<InfrastructureHealthResponse>
{
    /// <summary>
    /// Optional service name to filter health check.
    /// If null, returns all services health.
    /// </summary>
    public string? ServiceName { get; init; }
}

/// <summary>
/// Response DTO for infrastructure health query.
/// </summary>
public record InfrastructureHealthResponse(
    OverallHealthStatus Overall,
    IReadOnlyCollection<ServiceHealthDto> Services);

/// <summary>
/// DTO for individual service health.
/// </summary>
public record ServiceHealthDto(
    string ServiceName,
    string State,
    string? ErrorMessage,
    DateTime CheckedAt,
    double ResponseTimeMs);
