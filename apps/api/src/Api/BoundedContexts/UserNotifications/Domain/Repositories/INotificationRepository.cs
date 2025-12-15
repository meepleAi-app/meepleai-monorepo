using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

/// <summary>
/// Repository interface for Notification aggregate.
/// Extends IRepository with notification-specific queries.
/// </summary>
internal interface INotificationRepository : IRepository<Notification, Guid>
{
    /// <summary>
    /// Gets notifications for a specific user with optional filtering.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="unreadOnly">If true, returns only unread notifications</param>
    /// <param name="limit">Maximum number of notifications to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(
        Guid userId,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of unread notifications for a user.
    /// Optimized query for badge display.
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications as read for a user.
    /// Bulk update operation for "clear all" functionality.
    /// </summary>
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
