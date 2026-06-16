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

    /// <summary>
    /// Returns <c>true</c> when a Notification already exists for the given (user, source domain event id)
    /// pair (issue #1937 / CF-1). Scoped per-user so a fan-out event (e.g. notify-all-admins) can
    /// legitimately produce N Notifications — one per recipient — while still blocking same-user
    /// re-dispatch on retry. Used by <c>NotificationDispatcher</c> to short-circuit duplicate dispatch
    /// — see <c>NotificationMessage.SourceEventId</c>.
    /// </summary>
    Task<bool> ExistsBySourceEventIdAsync(Guid userId, Guid sourceEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomic add-and-commit for in-app notifications guarded by the
    /// <c>(user_id, source_event_id)</c> UNIQUE pair (issue #2383 / ADR-068/072
    /// follow-up). Mirrors <c>LedgerEntryRepository.AddAndCommitAsync</c>
    /// (CF-2 #1938): tracks the entity, calls <c>SaveChangesAsync</c>, and
    /// catches the Postgres <c>23505 unique_violation</c> race-window so the
    /// caller doesn't have to wrap every <c>INotificationHandler</c>
    /// invocation in a try/catch (10 of 25 handlers have no catch-all).
    /// </summary>
    /// <returns>
    /// <c>true</c> when the notification was persisted; <c>false</c> when a
    /// concurrent caller already inserted a row with the same source event id
    /// (dedup-by-race — the in-app row exists, just under the other caller).
    /// </returns>
    Task<bool> AddAndCommitAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch add-and-commit for fan-out notification callers (admin alerts,
    /// achievement unlocks, scheduled jobs that emit one row per user). Issue
    /// #2392 (phantom-broadcast follow-up): tracks every <paramref name="notifications"/>
    /// row, calls a single <c>SaveChangesAsync</c>, then fires side-effects
    /// (metric + SSE broadcast) AFTER the durable commit. Replaces the
    /// <c>foreach { AddAsync } + SaveChanges</c> pattern in 6 loop callers.
    /// </summary>
    /// <remarks>
    /// No 23505 race-window catch — batch callers don't set
    /// <c>SourceEventId</c>, so the partial UNIQUE index doesn't apply.
    /// Any <c>DbUpdateException</c> rolls back the entire batch.
    /// </remarks>
    /// <returns>Number of rows persisted (0 when input is empty).</returns>
    Task<int> AddBatchAndCommitAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);
}
