using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get API requests grouped by day for charts.
/// Issue #2790: Admin Dashboard Charts - API Requests BarChart
/// </summary>
internal record GetApiRequestsByDayQuery(
    int Days = 7
) : IQuery<ApiRequestByDayDto[]>;
