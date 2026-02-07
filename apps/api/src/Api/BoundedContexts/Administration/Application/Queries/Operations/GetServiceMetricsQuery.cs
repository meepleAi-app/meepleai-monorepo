using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Operations;

/// <summary>
/// Query to retrieve performance metrics for all monitored services.
/// Issue #3696: Operations - Service Control Panel.
/// Includes uptime, latency, and request count statistics.
/// </summary>
internal record GetServiceMetricsQuery() : IQuery<ServiceMetricsResponseDto>;
