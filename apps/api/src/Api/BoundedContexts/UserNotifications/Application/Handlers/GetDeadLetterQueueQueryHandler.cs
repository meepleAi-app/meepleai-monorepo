using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for GetDeadLetterQueueQuery.
/// Returns paginated dead letter queue items for admin retry management.
/// </summary>
internal class GetDeadLetterQueueQueryHandler
    : IQueryHandler<GetDeadLetterQueueQuery, PaginatedNotificationQueueResult>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetDeadLetterQueueQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedNotificationQueueResult> Handle(
        GetDeadLetterQueueQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.Set<NotificationQueueEntity>()
            .AsNoTracking()
            .Where(e => e.Status == "dead_letter");

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
