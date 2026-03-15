using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of NotificationQueueItem repository.
/// Maps between domain NotificationQueueItem aggregate and NotificationQueueEntity persistence model.
/// Handles polymorphic INotificationPayload serialization via NotificationPayloadSerializer.
/// </summary>
internal class NotificationQueueRepository : RepositoryBase, INotificationQueueRepository
{
    public NotificationQueueRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(NotificationQueueItem item, CancellationToken ct = default)
    {
        CollectDomainEvents(item);
        var entity = MapToPersistence(item);
        await DbContext.Set<NotificationQueueEntity>().AddAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task AddRangeAsync(IEnumerable<NotificationQueueItem> items, CancellationToken ct = default)
    {
        var entities = new List<NotificationQueueEntity>();
        foreach (var item in items)
        {
            CollectDomainEvents(item);
            entities.Add(MapToPersistence(item));
        }

        await DbContext.Set<NotificationQueueEntity>().AddRangeAsync(entities, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(NotificationQueueItem item, CancellationToken ct = default)
    {
        CollectDomainEvents(item);
        var entity = MapToPersistence(item);

        var tracked = DbContext.ChangeTracker.Entries<NotificationQueueEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
        {
            tracked.CurrentValues.SetValues(entity);
            tracked.State = EntityState.Modified;
        }
        else
        {
            DbContext.Set<NotificationQueueEntity>().Update(entity);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationQueueItem>> GetPendingByChannelAsync(
        NotificationChannelType channelType, int batchSize, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var channelValue = channelType.Value;

        var entities = await DbContext.Set<NotificationQueueEntity>()
            .AsNoTracking()
            .Where(e =>
                e.ChannelType == channelValue &&
                ((e.Status == "pending") ||
                 (e.Status == "failed" && e.NextRetryAt != null && e.NextRetryAt <= now)))
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await DbContext.Set<NotificationQueueEntity>()
            .AsNoTracking()
            .CountAsync(e =>
                (e.Status == "pending") ||
                (e.Status == "failed" && e.NextRetryAt != null && e.NextRetryAt <= now),
                ct).ConfigureAwait(false);
    }

    public async Task<int> GetDeadLetterCountAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<NotificationQueueEntity>()
            .AsNoTracking()
            .CountAsync(e => e.Status == "dead_letter", ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationQueueItem>> GetDeadLetterItemsAsync(
        int batchSize, CancellationToken ct = default)
    {
        var entities = await DbContext.Set<NotificationQueueEntity>()
            .AsNoTracking()
            .Where(e => e.Status == "dead_letter")
            .OrderByDescending(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    private static NotificationQueueEntity MapToPersistence(NotificationQueueItem item)
    {
        return new NotificationQueueEntity
        {
            Id = item.Id,
            ChannelType = item.ChannelType.Value,
            RecipientUserId = item.RecipientUserId,
            NotificationType = item.NotificationType.Value,
            Payload = JsonSerializer.Serialize<INotificationPayload>(item.Payload, NotificationPayloadSerializer.CreateOptions()),
            SlackChannelTarget = item.SlackChannelTarget,
            SlackTeamId = item.SlackTeamId,
            Status = item.Status.Value,
            RetryCount = item.RetryCount,
            MaxRetries = item.MaxRetries,
            NextRetryAt = item.NextRetryAt,
            LastError = item.LastError,
            CreatedAt = item.CreatedAt,
            ProcessedAt = item.ProcessedAt,
            CorrelationId = item.CorrelationId
        };
    }

    private static NotificationQueueItem MapToDomain(NotificationQueueEntity entity)
    {
        return NotificationQueueItem.Reconstitute(
            id: entity.Id,
            channelType: NotificationChannelType.FromString(entity.ChannelType),
            recipientUserId: entity.RecipientUserId,
            notificationType: NotificationType.FromString(entity.NotificationType),
            payload: JsonSerializer.Deserialize<INotificationPayload>(entity.Payload, NotificationPayloadSerializer.CreateOptions())!,
            slackChannelTarget: entity.SlackChannelTarget,
            slackTeamId: entity.SlackTeamId,
            status: NotificationQueueStatus.FromString(entity.Status),
            retryCount: entity.RetryCount,
            maxRetries: entity.MaxRetries,
            nextRetryAt: entity.NextRetryAt,
            lastError: entity.LastError,
            createdAt: entity.CreatedAt,
            processedAt: entity.ProcessedAt,
            correlationId: entity.CorrelationId);
    }
}
