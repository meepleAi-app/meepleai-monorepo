using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetAdminStatsQuery.
/// Delegates to AdminStatsService (service will be refactored in future iterations).
/// </summary>
public class GetAdminStatsQueryHandler : IQueryHandler<GetAdminStatsQuery, DashboardStatsDto>
{
    private readonly IAdminStatsService _adminStatsService;

    public GetAdminStatsQueryHandler(IAdminStatsService adminStatsService)
    {
        _adminStatsService = adminStatsService ?? throw new ArgumentNullException(nameof(adminStatsService));
    }

    public async Task<DashboardStatsDto> Handle(GetAdminStatsQuery query, CancellationToken cancellationToken)
    {
        var queryParams = new AnalyticsQueryParams(
            FromDate: query.FromDate,
            ToDate: query.ToDate,
            Days: query.Days,
            GameId: query.GameId,
            RoleFilter: query.RoleFilter
        );

        return await _adminStatsService.GetDashboardStatsAsync(queryParams, cancellationToken).ConfigureAwait(false);
    }
}
