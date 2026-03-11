using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserNotifications.Domain.Aggregates;

/// <summary>
/// Aggregate root representing an email in the async queue.
/// Manages email lifecycle: pending, processing, sent, failed, dead letter.
/// Implements exponential backoff retry policy (1m, 5m, 30m).
/// Issue #4417: Email notification queue with retry policy.
/// </summary>
internal sealed class EmailQueueItem : AggregateRoot<Guid>
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30)
    ];

    public Guid UserId { get; private set; }
    public string To { get; private set; }
    public string Subject { get; private set; }
    public string HtmlBody { get; private set; }
    public EmailQueueStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public Guid? CorrelationId { get; private set; }

#pragma warning disable CS8618
    private EmailQueueItem() : base() { }
#pragma warning restore CS8618

    private EmailQueueItem(
        Guid id,
        Guid userId,
        string to,
        string subject,
        string htmlBody,
        Guid? correlationId = null)
        : base(id)
    {
        UserId = userId;
        To = !string.IsNullOrWhiteSpace(to) ? to : throw new ArgumentException("To address cannot be empty", nameof(to));
        Subject = !string.IsNullOrWhiteSpace(subject) ? subject : throw new ArgumentException("Subject cannot be empty", nameof(subject));
        HtmlBody = !string.IsNullOrWhiteSpace(htmlBody) ? htmlBody : throw new ArgumentException("HtmlBody cannot be empty", nameof(htmlBody));
        Status = EmailQueueStatus.Pending;
        RetryCount = 0;
        MaxRetries = 3;
        NextRetryAt = null;
        ErrorMessage = null;
        CreatedAt = DateTime.UtcNow;
        ProcessedAt = null;
        FailedAt = null;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Factory method to create a new email queue item.
    /// </summary>
    public static EmailQueueItem Create(Guid userId, string to, string subject, string htmlBody, Guid? correlationId = null)
    {
        return new EmailQueueItem(Guid.NewGuid(), userId, to, subject, htmlBody, correlationId);
    }

    /// <summary>
    /// Marks the email as currently being processed.
    /// Prevents duplicate pickup by other job executions.
    /// </summary>
    public void MarkAsProcessing()
    {
        if (!Status.IsPending && !Status.IsFailed)
            throw new InvalidOperationException($"Cannot process email in status '{Status.Value}'");

        Status = EmailQueueStatus.Processing;
    }

    /// <summary>
    /// Marks the email as successfully sent.
    /// </summary>
    public void MarkAsSent(DateTime processedAt)
    {
        if (!Status.IsProcessing)
            throw new InvalidOperationException($"Cannot mark as sent from status '{Status.Value}'");

        Status = EmailQueueStatus.Sent;
        ProcessedAt = processedAt;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marks the email as failed with exponential backoff retry scheduling.
    /// If max retries exceeded, moves to dead letter.
    /// </summary>
    public void MarkAsFailed(string error)
    {
        if (!Status.IsProcessing)
            throw new InvalidOperationException($"Cannot mark as failed from status '{Status.Value}'");

        RetryCount++;
        ErrorMessage = error;
        FailedAt = DateTime.UtcNow;

        if (RetryCount >= MaxRetries)
        {
            MarkAsDeadLetter();
            return;
        }

        Status = EmailQueueStatus.Failed;
        var delayIndex = Math.Min(RetryCount - 1, RetryDelays.Length - 1);
        NextRetryAt = DateTime.UtcNow.Add(RetryDelays[delayIndex]);
    }

    /// <summary>
    /// Moves the email to dead letter queue.
    /// No further retry attempts will be made.
    /// </summary>
    public void MarkAsDeadLetter()
    {
        Status = EmailQueueStatus.DeadLetter;
        NextRetryAt = null;
    }

    /// <summary>
    /// Resets a dead letter or failed email back to pending for manual retry.
    /// </summary>
    public void ResetToPending()
    {
        if (!Status.IsDeadLetter && !Status.IsFailed)
            throw new InvalidOperationException($"Cannot reset email in status '{Status.Value}'");

        Status = EmailQueueStatus.Pending;
        RetryCount = 0;
        NextRetryAt = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Reconstitutes the aggregate from persistence.
    /// Used by repository when mapping from database entity.
    /// </summary>
    internal static EmailQueueItem Reconstitute(
        Guid id,
        Guid userId,
        string to,
        string subject,
        string htmlBody,
        EmailQueueStatus status,
        int retryCount,
        int maxRetries,
        DateTime? nextRetryAt,
        string? errorMessage,
        DateTime createdAt,
        DateTime? processedAt,
        DateTime? failedAt,
        Guid? correlationId = null)
    {
        var item = new EmailQueueItem(id, userId, to, subject, htmlBody);
        item.Status = status;
        item.RetryCount = retryCount;
        item.MaxRetries = maxRetries;
        item.NextRetryAt = nextRetryAt;
        item.ErrorMessage = errorMessage;
        item.CreatedAt = createdAt;
        item.ProcessedAt = processedAt;
        item.FailedAt = failedAt;
        item.CorrelationId = correlationId;
        return item;
    }
}
