using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.BoundedContexts.UserNotifications.TestHelpers;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain;

/// <summary>
/// Tests for Notification aggregate root.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class NotificationTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesNotification()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var type = NotificationType.PdfUploadCompleted;
        var severity = NotificationSeverity.Success;
        var title = "Upload Complete";
        var message = "Your PDF has been processed.";

        // Act
        var notification = new Notification(
            id: id,
            userId: userId,
            type: type,
            severity: severity,
            title: title,
            message: message
        );

        // Assert
        Assert.Equal(id, notification.Id);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(type, notification.Type);
        Assert.Equal(severity, notification.Severity);
        Assert.Equal(title, notification.Title);
        Assert.Equal(message, notification.Message);
        Assert.Null(notification.Link);
        Assert.Null(notification.Metadata);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
        Assert.True(notification.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Constructor_WithOptionalParameters_SetsLinkAndMetadata()
    {
        // Arrange
        var link = "/chat/thread-123";
        var metadata = "{\"documentId\": \"doc-456\"}";

        // Act
        var notification = new NotificationBuilder()
            .WithLink(link)
            .WithMetadata(metadata)
            .Build();

        // Assert
        Assert.Equal(link, notification.Link);
        Assert.Equal(metadata, notification.Metadata);
    }

    [Fact]
    public void Constructor_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                type: NotificationType.PdfUploadCompleted,
                severity: NotificationSeverity.Success,
                title: "",
                message: "Valid message"
            ));

        Assert.Contains("Title", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyMessage_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                type: NotificationType.PdfUploadCompleted,
                severity: NotificationSeverity.Success,
                title: "Valid title",
                message: ""
            ));

        Assert.Contains("Message", exception.Message);
    }

    [Fact]
    public void Constructor_WithNullType_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                type: null!,
                severity: NotificationSeverity.Success,
                title: "Valid title",
                message: "Valid message"
            ));
    }

    [Fact]
    public void Constructor_WithNullSeverity_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                type: NotificationType.PdfUploadCompleted,
                severity: null!,
                title: "Valid title",
                message: "Valid message"
            ));
    }

    [Fact]
    public void MarkAsRead_WhenUnread_SetsIsReadAndReadAt()
    {
        // Arrange
        var notification = new NotificationBuilder().Build();
        var beforeRead = DateTime.UtcNow;

        // Act
        notification.MarkAsRead();
        var afterRead = DateTime.UtcNow;

        // Assert
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
        Assert.True(notification.ReadAt >= beforeRead);
        Assert.True(notification.ReadAt <= afterRead);
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_IsIdempotent()
    {
        // Arrange
        var notification = new NotificationBuilder().Build();
        notification.MarkAsRead();
        var firstReadAt = notification.ReadAt;

        // Act - Mark as read again
        Thread.Sleep(10); // Small delay to ensure different timestamp
        notification.MarkAsRead();

        // Assert - ReadAt should not change
        Assert.Equal(firstReadAt, notification.ReadAt);
    }

    [Fact]
    public void MarkAsUnread_WhenRead_ClearsIsReadAndReadAt()
    {
        // Arrange
        var notification = new NotificationBuilder().AsRead().Build();
        Assert.True(notification.IsRead);

        // Act
        notification.MarkAsUnread();

        // Assert
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void RestoreReadStatus_WithTimestamp_PreservesOriginalReadAt()
    {
        // Arrange
        var notification = new NotificationBuilder().Build();
        var originalReadAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        notification.RestoreReadStatus(originalReadAt);

        // Assert
        Assert.True(notification.IsRead);
        Assert.Equal(originalReadAt, notification.ReadAt);
    }

    [Fact]
    public void RestoreReadStatus_DoesNotOverwriteTimestamp()
    {
        // Arrange
        var notification = new NotificationBuilder().Build();
        var historicalTimestamp = DateTime.UtcNow.AddDays(-7);

        // Act
        notification.RestoreReadStatus(historicalTimestamp);

        // Assert - Should preserve exact timestamp, not use DateTime.UtcNow
        Assert.Equal(historicalTimestamp, notification.ReadAt);
    }
}
