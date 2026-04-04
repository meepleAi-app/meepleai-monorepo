using Api.BoundedContexts.Administration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Operations;

/// <summary>
/// Query to retrieve the enhanced service dashboard with uptime, trends, and categories.
/// Issue #132: Enhanced ServiceHealthMatrix.
/// </summary>
internal record GetServiceDashboardQuery : IRequest<EnhancedServiceDashboardDto>;
