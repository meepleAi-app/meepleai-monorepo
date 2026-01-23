using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetUserDashboardQuery (Issue #2854).
/// Delegates to UserDashboardService for business logic and caching.
/// </summary>
internal class GetUserDashboardQueryHandler : IQueryHandler<GetUserDashboardQuery, UserDashboardDto>
{
    private readonly IUserDashboardService _userDashboardService;
    private readonly ILogger<GetUserDashboardQueryHandler> _logger;

    public GetUserDashboardQueryHandler(
        IUserDashboardService userDashboardService,
        ILogger<GetUserDashboardQueryHandler> logger)
    {
        _userDashboardService = userDashboardService;
        _logger = logger;
    }

    public async Task<UserDashboardDto> Handle(
        GetUserDashboardQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching dashboard data for user {UserId}", query.UserId);

        var dashboard = await _userDashboardService
            .GetUserDashboardAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Dashboard data retrieved for user {UserId}: {RecentGames} games, {ActiveSessions} sessions, {RecentChats} chats",
            query.UserId,
            dashboard.RecentGames.Count,
            dashboard.ActiveSessions.Count,
            dashboard.RecentChats.Count);

        return dashboard;
    }
}
