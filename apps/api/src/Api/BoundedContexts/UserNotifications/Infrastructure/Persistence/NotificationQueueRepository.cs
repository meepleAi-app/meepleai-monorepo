using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of NotificationQueueItem repository.
/// Maps between domain NotificationQueueItem aggregate and NotificationQueueEntity persistence model.
/// Handles polymorphic INotificationPayload serialization via NotificationPayloadSerializer.
/// </summary>
internal class NotificationQueueRepository : RepositoryBase, INotificationQueueRepository
{
    private readonly ILogger<NotificationQueueRepository> _logger;

    public NotificationQueueRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<NotificationQueueRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var (items, poisonIds) = MaterializeResilient(entities);
        await DeadLetterUnmappableAsync(poisonIds, ct).ConfigureAwait(false);
        return items;
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

    public async Task<int> GetPendingCountByChannelsAsync(
        IReadOnlyCollection<NotificationChannelType> channels, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var channelValues = channels.Select(c => c.Value).ToArray();

        return await DbContext.Set<NotificationQueueEntity>()
            .AsNoTracking()
            .CountAsync(e =>
                channelValues.Contains(e.ChannelType) &&
                ((e.Status == "pending") ||
                 (e.Status == "failed" && e.NextRetryAt != null && e.NextRetryAt <= now)),
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

        // These rows are already dead-lettered; an unmappable one is just skipped (no re-dead-letter).
        var (items, _) = MaterializeResilient(entities);
        return items;
    }

    /// <summary>
    /// Materializes entities to domain items, isolating any row whose payload cannot be
    /// deserialized (unknown "$type", corrupt JSON, …) so a single poison row can never fail the
    /// whole batch — the root cause of the #3057 email-notification outage, where one malformed
    /// row made the entire <c>GetPendingByChannelAsync</c> query throw and no email was processed.
    /// Returns the successfully mapped items plus the ids of the rows that failed to map.
    /// </summary>
    private (List<NotificationQueueItem> Items, List<Guid> PoisonIds) MaterializeResilient(
        IReadOnlyList<NotificationQueueEntity> entities)
    {
        var items = new List<NotificationQueueItem>(entities.Count);
        var poisonIds = new List<Guid>();

        foreach (var entity in entities)
        {
            try
            {
                items.Add(MapToDomain(entity));
            }
#pragma warning disable CA1031 // one malformed row must not abort the batch — isolate + dead-letter it
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(
                    ex,
                    "notification_queue_items {NotificationQueueItemId} could not be materialized (payload deserialization failed); dead-lettering it so it cannot block the batch",
                    entity.Id);
                poisonIds.Add(entity.Id);
            }
        }

        return (items, poisonIds);
    }

    /// <summary>
    /// Best-effort dead-letters rows that could not be materialized, via a targeted UPDATE — the
    /// domain aggregate cannot be reconstituted from an undeserializable payload, so this bypasses
    /// the aggregate. Stops a poison row from being re-queried (and re-logged) every cycle. #3057.
    /// </summary>
    private async Task DeadLetterUnmappableAsync(IReadOnlyCollection<Guid> poisonIds, CancellationToken ct)
    {
        if (poisonIds.Count == 0)
        {
            return;
        }

        await DbContext.Set<NotificationQueueEntity>()
            .Where(e => poisonIds.Contains(e.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, "dead_letter")
                .SetProperty(e => e.LastError, "Payload could not be deserialized (isolated by #3057 guard)"),
                ct)
            .ConfigureAwait(false);
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
            CorrelationId = item.CorrelationId,
            DeepLinkPath = item.DeepLinkPath,
            SourceEventId = item.SourceEventId
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
            correlationId: entity.CorrelationId,
            deepLinkPath: entity.DeepLinkPath,
            sourceEventId: entity.SourceEventId);
    }
}
