using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Observability;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of Notification repository.
/// Maps between domain Notification aggregate and NotificationEntity persistence model.
/// </summary>
internal class NotificationRepository : RepositoryBase, INotificationRepository
{
    public NotificationRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
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
            ReadAt = notification.ReadAt
        };
    }

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
            metadata: entity.Metadata
        );

        // Restore read status with original timestamp (not MarkAsRead which overwrites)
        if (entity.IsRead && entity.ReadAt.HasValue)
        {
            notification.RestoreReadStatus(entity.ReadAt.Value);
        }

        return notification;
    }
}
