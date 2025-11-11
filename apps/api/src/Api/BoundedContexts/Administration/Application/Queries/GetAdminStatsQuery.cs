using Api.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get comprehensive dashboard statistics with caching.
/// </summary>
public record GetAdminStatsQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Days = 30,
    string? GameId = null,
    string? RoleFilter = null
) : IRequest<DashboardStatsDto>;
