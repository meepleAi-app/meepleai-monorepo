using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetSlackConnectionsQuery.
/// Returns paginated Slack connections for admin management.
/// Excludes sensitive bot access tokens from the response.
/// </summary>
internal class GetSlackConnectionsQueryHandler
    : IQueryHandler<GetSlackConnectionsQuery, PaginatedSlackConnectionsResult>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetSlackConnectionsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedSlackConnectionsResult> Handle(
        GetSlackConnectionsQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.Set<SlackConnectionEntity>().AsNoTracking();

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var skip = (query.Page - 1) * query.PageSize;
        var items = await dbQuery
            .OrderByDescending(e => e.ConnectedAt)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(e => new SlackConnectionDto(
                e.Id, e.UserId, e.SlackUserId, e.SlackTeamId,
                e.SlackTeamName, e.DmChannelId, e.IsActive,
                e.ConnectedAt, e.DisconnectedAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PaginatedSlackConnectionsResult(items, totalCount, query.Page, query.PageSize);
    }
}
