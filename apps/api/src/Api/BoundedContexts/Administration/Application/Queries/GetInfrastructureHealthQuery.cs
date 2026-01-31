using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get infrastructure health status.
/// Issue #891: Infrastructure monitoring service.
/// </summary>
internal record GetInfrastructureHealthQuery : IRequest<InfrastructureHealthResponse>
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
internal record InfrastructureHealthResponse(
    OverallHealthStatus Overall,
    IReadOnlyCollection<ServiceHealthDto> Services);

/// <summary>
/// DTO for individual service health.
/// </summary>
internal record ServiceHealthDto(
    string ServiceName,
    string State,
    string? ErrorMessage,
    DateTime CheckedAt,
    double ResponseTimeMs);
