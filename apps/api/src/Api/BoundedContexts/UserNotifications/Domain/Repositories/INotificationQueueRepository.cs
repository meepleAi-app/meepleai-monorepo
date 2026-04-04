using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

internal interface INotificationQueueRepository
{
    Task AddAsync(NotificationQueueItem item, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<NotificationQueueItem> items, CancellationToken ct = default);
    Task UpdateAsync(NotificationQueueItem item, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationQueueItem>> GetPendingByChannelAsync(
        NotificationChannelType channelType, int batchSize, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task<int> GetDeadLetterCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationQueueItem>> GetDeadLetterItemsAsync(
        int batchSize, CancellationToken ct = default);
}
