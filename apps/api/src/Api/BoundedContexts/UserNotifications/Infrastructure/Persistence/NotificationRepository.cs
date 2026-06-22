using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Observability;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of Notification repository.
/// Maps between domain Notification aggregate and NotificationEntity persistence model.
/// </summary>
internal class NotificationRepository : RepositoryBase, INotificationRepository
{
    private readonly IUserNotificationBroadcaster _broadcaster;

    public NotificationRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        IUserNotificationBroadcaster broadcaster)
        : base(dbContext, eventCollector)
    {
        _broadcaster = broadcaster;
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notificationEntity = await DbContext.Set<NotificationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken).ConfigureAwait(false);

        return notificationEntity != null ? MapToDomain(notificationEntity) : null;
    }

    public async Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var notificationEntities = await DbContext.Set<NotificationEntity>()
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return notificationEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(
        Guid userId,
        bool unreadOnly = false,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<NotificationEntity>()
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notificationEntities = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return notificationEntities.Select(MapToDomain).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<NotificationEntity>()
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<NotificationEntity>()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.UtcNow),
                cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(notification);

        var notificationEntity = MapToPersistence(notification);
        await DbContext.Set<NotificationEntity>().AddAsync(notificationEntity, cancellationToken).ConfigureAwait(false);

        // Record metric for notification creation
        MeepleAiMetrics.RecordNotificationCreated(notification.Type.Value, notification.Severity.Value);

        // Broadcast to connected SSE clients (Issue #5005).
        // Fires before UnitOfWork.SaveChangesAsync — matches metric recording pattern.
        //
        // Phantom-broadcast risk (acknowledged): if the caller's external
        // SaveChangesAsync subsequently throws (FK violation on a sibling entity,
        // deadlock, late constraint check), the metric is already counted and
        // the SSE frame has already shipped for a notification that was never
        // durably stored. Issue #2392 migrated 9 of 10 callers to
        // <see cref="AddAndCommitAsync"/> (single-row) / <see cref="AddBatchAndCommitAsync"/>
        // (fan-out batch). The one remaining call site —
        // <c>SlackNotificationProcessorJob.HandleTokenRevocationAsync</c> — keeps
        // <c>AddAsync</c> on purpose: its outer save deactivates the Slack
        // connection AND inserts the notification atomically, and splitting
        // them would create a worse failure mode (deactivated connection with
        // no user notification) than the residual phantom risk. New callers
        // SHOULD prefer the AddAndCommit variants.
        _broadcaster.Publish(notification.UserId, MapToDto(notification));
    }

    /// <inheritdoc />
    public async Task<int> AddBatchAndCommitAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        var list = notifications.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        foreach (var n in list)
        {
            CollectDomainEvents(n);
            var entity = MapToPersistence(n);
            await DbContext.Set<NotificationEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        // Single SaveChangesAsync for the whole batch — preserves atomicity
        // (all rows persist together or none do via Postgres transaction).
        // Any DbUpdateException bubbles up unchanged; side-effects below
        // are skipped, so no phantom broadcast / metric fires.
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // POST-save side-effects (issue #2392): once persisted, fire the
        // metric counter and SSE frame for each row. Iteration order matches
        // the input list so callers can reason about per-row notification.
        foreach (var n in list)
        {
            MeepleAiMetrics.RecordNotificationCreated(n.Type.Value, n.Severity.Value);
            _broadcaster.Publish(n.UserId, MapToDto(n));
        }

        return list.Count;
    }

    /// <inheritdoc />
    public async Task<bool> AddAndCommitAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        CollectDomainEvents(notification);

        var notificationEntity = MapToPersistence(notification);
        await DbContext.Set<NotificationEntity>().AddAsync(notificationEntity, cancellationToken).ConfigureAwait(false);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (CounterTableIdempotency.IsUniqueViolation(ex))
        {
            // Race-window: concurrent caller already inserted a row with the same
            // (user_id, source_event_id) UNIQUE pair (issue #2383). Detach the
            // unsaved entity so it isn't retried on a later SaveChangesAsync,
            // and signal the caller (dispatcher) to skip channel queue items —
            // the in-app row exists already, just under the other caller's CorrelationId.
            DbContext.Entry(notificationEntity).State = EntityState.Detached;
            return false;
        }

        // Side-effects fire only after the row is durably persisted so a lost
        // race doesn't leave a phantom metric/SSE broadcast for a notification
        // that was never committed.
        MeepleAiMetrics.RecordNotificationCreated(notification.Type.Value, notification.Severity.Value);
        _broadcaster.Publish(notification.UserId, MapToDto(notification));

        return true;
    }

    public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE updating
        CollectDomainEvents(notification);

        var notificationEntity = MapToPersistence(notification);
        DbContext.Set<NotificationEntity>().Update(notificationEntity);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);


        await DbContext.Set<NotificationEntity>()
            .Where(n => n.Id == notification.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<NotificationEntity>()
            .AsNoTracking()
            .AnyAsync(n => n.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsBySourceEventIdAsync(Guid userId, Guid sourceEventId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<NotificationEntity>()
            .AsNoTracking()
            .AnyAsync(n => n.UserId == userId && n.SourceEventId == sourceEventId, cancellationToken).ConfigureAwait(false);
    }

    // Domain → Persistence mapping
    private static NotificationEntity MapToPersistence(Notification notification)
    {
        return new NotificationEntity
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Type = notification.Type.Value,
            Severity = notification.Severity.Value,
            Title = notification.Title,
            Message = notification.Message,
            Link = notification.Link,
            Metadata = notification.Metadata,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            CorrelationId = notification.CorrelationId,
            SourceEventId = notification.SourceEventId
        };
    }

    // Domain → DTO mapping (used for SSE broadcasting)
    private static NotificationDto MapToDto(Notification notification) =>
        new(
            notification.Id,
            notification.UserId,
            notification.Type.Value,
            notification.Severity.Value,
            notification.Title,
            notification.Message,
            notification.Link,
            notification.Metadata,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt,
            notification.CorrelationId
        );

    // Persistence → Domain mapping
    private static Notification MapToDomain(NotificationEntity entity)
    {
        var notification = new Notification(
            id: entity.Id,
            userId: entity.UserId,
            type: NotificationType.FromString(entity.Type),
            severity: NotificationSeverity.FromString(entity.Severity),
            title: entity.Title,
            message: entity.Message,
            link: entity.Link,
            metadata: entity.Metadata,
            correlationId: entity.CorrelationId,
            sourceEventId: entity.SourceEventId
        );

        // Restore read status with original timestamp (not MarkAsRead which overwrites)
        if (entity.IsRead && entity.ReadAt.HasValue)
        {
            notification.RestoreReadStatus(entity.ReadAt.Value);
        }

        return notification;
    }
}
