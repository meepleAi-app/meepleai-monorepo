using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetNotificationMetricsQuery.
/// Returns aggregated notification queue metrics grouped by channel and status.
/// </summary>
internal class GetNotificationMetricsQueryHandler
    : IQueryHandler<GetNotificationMetricsQuery, NotificationMetricsDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetNotificationMetricsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<NotificationMetricsDto> Handle(
        GetNotificationMetricsQuery query, CancellationToken cancellationToken)
    {
        var items = _dbContext.Set<NotificationQueueEntity>().AsNoTracking();

        var totalPending = await items.CountAsync(e => e.Status == "pending", cancellationToken).ConfigureAwait(false);
        var totalSent = await items.CountAsync(e => e.Status == "sent", cancellationToken).ConfigureAwait(false);
        var totalFailed = await items.CountAsync(e => e.Status == "failed", cancellationToken).ConfigureAwait(false);
        var totalDeadLetter = await items.CountAsync(e => e.Status == "dead_letter", cancellationToken).ConfigureAwait(false);

        var pendingByChannel = await items
            .Where(e => e.Status == "pending")
            .GroupBy(e => e.ChannelType)
            .Select(g => new { Channel = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Channel, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        return new NotificationMetricsDto(
            totalPending, totalSent, totalFailed, totalDeadLetter, pendingByChannel);
    }
}
