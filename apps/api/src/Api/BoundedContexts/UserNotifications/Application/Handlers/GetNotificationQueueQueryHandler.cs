using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for GetNotificationQueueQuery.
/// Returns paginated notification queue items with optional channel and status filtering.
/// </summary>
internal class GetNotificationQueueQueryHandler
    : IQueryHandler<GetNotificationQueueQuery, PaginatedNotificationQueueResult>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetNotificationQueueQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedNotificationQueueResult> Handle(
        GetNotificationQueueQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.Set<NotificationQueueEntity>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.ChannelFilter))
        {
            dbQuery = dbQuery.Where(e => e.ChannelType == query.ChannelFilter);
        }

        if (!string.IsNullOrWhiteSpace(query.StatusFilter))
        {
            dbQuery = dbQuery.Where(e => e.Status == query.StatusFilter);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var skip = (query.Page - 1) * query.PageSize;
        var items = await dbQuery
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(e => new NotificationQueueItemDto(
                e.Id, e.ChannelType, e.NotificationType, e.Status,
                e.RetryCount, e.LastError, e.CreatedAt, e.ProcessedAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PaginatedNotificationQueueResult(items, totalCount, query.Page, query.PageSize);
    }
}
