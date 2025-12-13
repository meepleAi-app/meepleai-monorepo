using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.UserNotifications.TestHelpers;

/// <summary>
/// Builder for creating Notification test fixtures.
/// </summary>
public class NotificationBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _userId = Guid.NewGuid();
    private NotificationType _type = NotificationType.PdfUploadCompleted;
    private NotificationSeverity _severity = NotificationSeverity.Success;
    private string _title = "Test Notification";
    private string _message = "This is a test notification message.";
    private string? _link = null;
    private string? _metadata = null;
    private bool _isRead = false;

    public NotificationBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public NotificationBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public NotificationBuilder WithType(NotificationType type)
    {
        _type = type;
        return this;
    }

    public NotificationBuilder WithSeverity(NotificationSeverity severity)
    {
        _severity = severity;
        return this;
    }

    public NotificationBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public NotificationBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    public NotificationBuilder WithLink(string? link)
    {
        _link = link;
        return this;
    }

    public NotificationBuilder WithMetadata(string? metadata)
    {
        _metadata = metadata;
        return this;
    }

    public NotificationBuilder AsRead()
    {
        _isRead = true;
        return this;
    }

    public Notification Build()
    {
        var notification = new Notification(
            id: _id,
            userId: _userId,
            type: _type,
            severity: _severity,
            title: _title,
            message: _message,
            link: _link,
            metadata: _metadata
        );

        if (_isRead)
        {
            notification.MarkAsRead();
        }

        return notification;
    }
}
