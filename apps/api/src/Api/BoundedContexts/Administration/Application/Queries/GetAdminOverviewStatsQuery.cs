using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Lightweight query for admin overview statistics.
/// Issue #4198: Returns only the stats needed by StatsOverview component.
/// </summary>
internal record GetAdminOverviewStatsQuery : IQuery<AdminOverviewStatsDto>;
