using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Operations;

/// <summary>
/// Query to retrieve aggregated health status for all monitored services.
/// Issue #3696: Operations - Service Control Panel.
/// Reuses existing health check infrastructure from /api/v1/health.
/// </summary>
internal record GetServiceHealthQuery() : IQuery<ServiceHealthResponseDto>;
