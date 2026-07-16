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
    /// <summary>
    /// Counts pending (or retry-due failed) queue items limited to the given channels.
    /// Uses the same "pending" predicate as <see cref="GetPendingCountAsync"/> but scoped
    /// to a set of channels, so per-channel health checks are not tripped by an unrelated
    /// backlog on another channel (e.g. an email backlog must not fail the Slack check).
    /// </summary>
    Task<int> GetPendingCountByChannelsAsync(
        IReadOnlyCollection<NotificationChannelType> channels, CancellationToken ct = default);
    Task<int> GetDeadLetterCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationQueueItem>> GetDeadLetterItemsAsync(
        int batchSize, CancellationToken ct = default);
}
