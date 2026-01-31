using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get comprehensive dashboard statistics with caching.
/// </summary>
internal record GetAdminStatsQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Days = 30,
    string? GameId = null,
    string? RoleFilter = null
) : IQuery<DashboardStatsDto>;
