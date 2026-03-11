using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserNotifications.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a user notification.
/// Tracks notification lifecycle: creation, read status, and metadata.
/// </summary>
internal sealed class Notification : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationSeverity Severity { get; private set; }
    public string Title { get; private set; }
    public string Message { get; private set; }
    public string? Link { get; private set; }
    public string? Metadata { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public Guid? CorrelationId { get; private set; }

#pragma warning disable CS8618
    private Notification() : base() { }
#pragma warning restore CS8618

    /// <summary>
    /// Creates a new notification for a user.
    /// </summary>
    /// <param name="id">Notification unique identifier</param>
    /// <param name="userId">User who receives the notification</param>
    /// <param name="type">Notification type (event category)</param>
    /// <param name="severity">Severity level for UI presentation</param>
    /// <param name="title">Brief notification title</param>
    /// <param name="message">Detailed notification message</param>
    /// <param name="link">Optional deep-link target (e.g., /chat/thread-id)</param>
    /// <param name="metadata">Optional JSON metadata for additional context</param>
    /// <param name="correlationId">Optional correlation ID for cross-channel tracking</param>
    public Notification(
        Guid id,
        Guid userId,
        NotificationType type,
        NotificationSeverity severity,
        string title,
        string message,
        string? link = null,
        string? metadata = null,
        Guid? correlationId = null)
        : base(id)
    {
        UserId = userId;
        ArgumentNullException.ThrowIfNull(type);
        Type = type;
        ArgumentNullException.ThrowIfNull(severity);
        Severity = severity;
        Title = !string.IsNullOrWhiteSpace(title) ? title : throw new ArgumentException("Title cannot be empty", nameof(title));
        Message = !string.IsNullOrWhiteSpace(message) ? message : throw new ArgumentException("Message cannot be empty", nameof(message));
        Link = link;
        Metadata = metadata;
        CorrelationId = correlationId;
        IsRead = false;
        CreatedAt = DateTime.UtcNow;
        ReadAt = null;
    }

    /// <summary>
    /// Marks notification as read.
    /// Idempotent operation - can be called multiple times safely.
    /// </summary>
    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Restores read status from persistence.
    /// Used by repository when reconstituting aggregate from database.
    /// Preserves original read timestamp instead of overwriting with current time.
    /// </summary>
    /// <param name="readAt">The original timestamp when notification was read</param>
    internal void RestoreReadStatus(DateTime readAt)
    {
        IsRead = true;
        ReadAt = readAt;
    }

    /// <summary>
    /// Marks notification as unread (for testing or user preference).
    /// </summary>
    public void MarkAsUnread()
    {
        IsRead = false;
        ReadAt = null;
    }
}
