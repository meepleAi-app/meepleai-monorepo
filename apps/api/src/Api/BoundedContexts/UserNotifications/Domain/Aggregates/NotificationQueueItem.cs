using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserNotifications.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a notification in the async queue.
/// Manages notification lifecycle: pending, processing, sent, failed, dead letter.
/// Supports multiple channels (email, Slack DM, Slack team) with exponential backoff retry (1m, 5m, 30m).
/// </summary>
internal sealed class NotificationQueueItem : AggregateRoot<Guid>
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30)
    ];

    public NotificationChannelType ChannelType { get; private set; }
    public Guid? RecipientUserId { get; private set; }
    public NotificationType NotificationType { get; private set; }
    public INotificationPayload Payload { get; private set; }
    public string? SlackChannelTarget { get; private set; }
    public string? SlackTeamId { get; private set; }
    public NotificationQueueStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Guid CorrelationId { get; private set; }

#pragma warning disable CS8618
    private NotificationQueueItem() : base() { }
#pragma warning restore CS8618

    private NotificationQueueItem(
        Guid id,
        NotificationChannelType channelType,
        Guid? recipientUserId,
        NotificationType notificationType,
        INotificationPayload payload,
        string? slackChannelTarget,
        string? slackTeamId,
        Guid correlationId,
        DateTime? createdAt = null)
        : base(id)
    {
        ChannelType = channelType ?? throw new ArgumentNullException(nameof(channelType));
        RecipientUserId = recipientUserId;
        NotificationType = notificationType ?? throw new ArgumentNullException(nameof(notificationType));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        SlackChannelTarget = slackChannelTarget;
        SlackTeamId = slackTeamId;
        Status = NotificationQueueStatus.Pending;
        RetryCount = 0;
        MaxRetries = 3;
        NextRetryAt = null;
        LastError = null;
        CreatedAt = createdAt ?? DateTime.UtcNow;
        ProcessedAt = null;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Factory method to create a new notification queue item.
    /// </summary>
    public static NotificationQueueItem Create(
        NotificationChannelType channelType,
        Guid? recipientUserId,
        NotificationType notificationType,
        INotificationPayload payload,
        string? slackChannelTarget = null,
        string? slackTeamId = null,
        Guid? correlationId = null,
        DateTime? createdAt = null)
    {
        return new NotificationQueueItem(
            Guid.NewGuid(),
            channelType,
            recipientUserId,
            notificationType,
            payload,
            slackChannelTarget,
            slackTeamId,
            correlationId ?? Guid.NewGuid(),
            createdAt);
    }

    /// <summary>
    /// Marks the notification as currently being processed.
    /// Prevents duplicate pickup by other job executions.
    /// </summary>
    public void MarkAsProcessing()
    {
        if (!Status.IsPending && !Status.IsFailed)
            throw new InvalidOperationException($"Cannot process notification in status '{Status.Value}'");

        Status = NotificationQueueStatus.Processing;
    }

    /// <summary>
    /// Marks the notification as successfully sent.
    /// </summary>
    public void MarkAsSent(DateTime processedAt)
    {
        if (!Status.IsProcessing)
            throw new InvalidOperationException($"Cannot mark as sent from status '{Status.Value}'");

        Status = NotificationQueueStatus.Sent;
        ProcessedAt = processedAt;
        LastError = null;
    }

    /// <summary>
    /// Marks the notification as failed with exponential backoff retry scheduling.
    /// If max retries exceeded, moves to dead letter.
    /// </summary>
    public void MarkAsFailed(string errorMessage, DateTime? now = null)
    {
        if (!Status.IsProcessing)
            throw new InvalidOperationException($"Cannot mark as failed from status '{Status.Value}'");

        var utcNow = now ?? DateTime.UtcNow;

        RetryCount++;
        LastError = errorMessage;

        if (RetryCount > MaxRetries)
        {
            MarkAsDeadLetter(errorMessage);
            return;
        }

        Status = NotificationQueueStatus.Failed;
        var delayIndex = Math.Min(RetryCount - 1, RetryDelays.Length - 1);
        NextRetryAt = utcNow.Add(RetryDelays[delayIndex]);
    }

    /// <summary>
    /// Sets the next retry time explicitly, e.g. for rate-limit 429 Retry-After handling.
    /// </summary>
    public void SetNextRetryAt(DateTime nextRetry)
    {
        NextRetryAt = nextRetry;
    }

    /// <summary>
    /// Moves the notification to dead letter queue.
    /// No further retry attempts will be made.
    /// </summary>
    public void MarkAsDeadLetter(string reason)
    {
        Status = NotificationQueueStatus.DeadLetter;
        LastError = reason;
        NextRetryAt = null;
    }

    /// <summary>
    /// Reconstitutes the aggregate from persistence.
    /// Used by repository when mapping from database entity.
    /// </summary>
    internal static NotificationQueueItem Reconstitute(
        Guid id,
        NotificationChannelType channelType,
        Guid? recipientUserId,
        NotificationType notificationType,
        INotificationPayload payload,
        string? slackChannelTarget,
        string? slackTeamId,
        NotificationQueueStatus status,
        int retryCount,
        int maxRetries,
        DateTime? nextRetryAt,
        string? lastError,
        DateTime createdAt,
        DateTime? processedAt,
        Guid correlationId)
    {
        var item = new NotificationQueueItem(
            id, channelType, recipientUserId, notificationType,
            payload, slackChannelTarget, slackTeamId, correlationId);
        item.Status = status;
        item.RetryCount = retryCount;
        item.MaxRetries = maxRetries;
        item.NextRetryAt = nextRetryAt;
        item.LastError = lastError;
        item.CreatedAt = createdAt;
        item.ProcessedAt = processedAt;
        return item;
    }
}
