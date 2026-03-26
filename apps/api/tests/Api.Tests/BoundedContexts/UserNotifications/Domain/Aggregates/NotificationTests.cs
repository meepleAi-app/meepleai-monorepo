using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.Aggregates;

/// <summary>
/// Tests for the Notification aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 21 PR#5
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotificationTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesNotification()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var notification = new Notification(
            id,
            userId,
            NotificationType.PdfUploadCompleted,
            NotificationSeverity.Success,
            "Upload Complete",
            "Your PDF has been processed successfully");

        // Assert
        notification.Id.Should().Be(id);
        notification.UserId.Should().Be(userId);
        notification.Type.Should().Be(NotificationType.PdfUploadCompleted);
        notification.Severity.Should().Be(NotificationSeverity.Success);
        notification.Title.Should().Be("Upload Complete");
        notification.Message.Should().Be("Your PDF has been processed successfully");
        notification.Link.Should().BeNull();
        notification.Metadata.Should().BeNull();
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
        notification.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_WithLinkAndMetadata_SetsOptionalProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var link = "/documents/123";
        var metadata = "{\"documentId\":\"123\",\"pages\":5}";

        // Act
        var notification = new Notification(
            id,
            userId,
            NotificationType.RuleSpecGenerated,
            NotificationSeverity.Info,
            "Rules Generated",
            "Game rules specification has been generated",
            link,
            metadata);

        // Assert
        notification.Link.Should().Be(link);
        notification.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void Constructor_WithNullType_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null!,
            NotificationSeverity.Info,
            "Title",
            "Message");

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullSeverity_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.NewComment,
            null!,
            "Title",
            "Message");

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyTitle_ThrowsArgumentException()
    {
        // Act
        var action = () => new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.ProcessingFailed,
            NotificationSeverity.Error,
            "",
            "Message");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceTitle_ThrowsArgumentException()
    {
        // Act
        var action = () => new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.ProcessingFailed,
            NotificationSeverity.Error,
            "   ",
            "Message");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyMessage_ThrowsArgumentException()
    {
        // Act
        var action = () => new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.SharedLinkAccessed,
            NotificationSeverity.Info,
            "Title",
            "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("message")
            .WithMessage("*Message cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceMessage_ThrowsArgumentException()
    {
        // Act
        var action = () => new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.SharedLinkAccessed,
            NotificationSeverity.Info,
            "Title",
            "   ");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Message cannot be empty*");
    }

    #endregion

    #region MarkAsRead Tests

    [Fact]
    public void MarkAsRead_WhenUnread_SetsIsReadAndReadAt()
    {
        // Arrange
        var notification = CreateTestNotification();
        notification.IsRead.Should().BeFalse();

        // Act
        notification.MarkAsRead();

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
        notification.ReadAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkAsRead_WhenAlreadyRead_IsIdempotent()
    {
        // Arrange
        var notification = CreateTestNotification();
        notification.MarkAsRead();
        var firstReadAt = notification.ReadAt;
        await Task.Delay(50);

        // Act
        notification.MarkAsRead();

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().Be(firstReadAt); // Should not change
    }

    [Fact]
    public async Task MarkAsRead_CalledMultipleTimes_PreservesOriginalReadTimestamp()
    {
        // Arrange
        var notification = CreateTestNotification();
        notification.MarkAsRead();
        var originalReadAt = notification.ReadAt!.Value;
        await Task.Delay(50);

        // Act
        notification.MarkAsRead();
        notification.MarkAsRead();
        notification.MarkAsRead();

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().Be(originalReadAt);
    }

    #endregion

    #region MarkAsUnread Tests

    [Fact]
    public void MarkAsUnread_WhenRead_SetsIsReadFalseAndClearsReadAt()
    {
        // Arrange
        var notification = CreateTestNotification();
        notification.MarkAsRead();
        notification.IsRead.Should().BeTrue();

        // Act
        notification.MarkAsUnread();

        // Assert
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsUnread_WhenAlreadyUnread_IsIdempotent()
    {
        // Arrange
        var notification = CreateTestNotification();
        notification.IsRead.Should().BeFalse();

        // Act
        notification.MarkAsUnread();

        // Assert
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsUnread_AfterMarkAsRead_AllowsRereading()
    {
        // Arrange
        var notification = CreateTestNotification();
        notification.MarkAsRead();

        // Act
        notification.MarkAsUnread();
        notification.MarkAsRead();

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
    }

    #endregion

    #region RestoreReadStatus Tests

    [Fact]
    public void RestoreReadStatus_SetsIsReadAndPreservesTimestamp()
    {
        // Arrange
        var notification = CreateTestNotification();
        var readTimestamp = DateTime.UtcNow.AddHours(-2);

        // Act
        notification.RestoreReadStatus(readTimestamp);

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().Be(readTimestamp);
    }

    [Fact]
    public void RestoreReadStatus_WithPastTimestamp_PreservesHistoricalReadTime()
    {
        // Arrange
        var notification = CreateTestNotification();
        var historicalReadTime = DateTime.UtcNow.AddDays(-7);

        // Act
        notification.RestoreReadStatus(historicalReadTime);

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().Be(historicalReadTime);
        notification.ReadAt.Should().BeBefore(DateTime.UtcNow);
    }

    [Fact]
    public void RestoreReadStatus_DoesNotUseCurrentTime()
    {
        // Arrange
        var notification = CreateTestNotification();
        var specificTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        notification.RestoreReadStatus(specificTime);

        // Assert
        notification.ReadAt.Should().Be(specificTime);
        notification.ReadAt.Should().NotBeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Notification Type Scenarios

    [Fact]
    public void Notification_ForShareRequestApproved_CreatesWithCorrectType()
    {
        // Act
        var notification = new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.ShareRequestApproved,
            NotificationSeverity.Success,
            "Share Request Approved",
            "Your game share request has been approved");

        // Assert
        notification.Type.Should().Be(NotificationType.ShareRequestApproved);
        notification.Severity.Should().Be(NotificationSeverity.Success);
    }

    [Fact]
    public void Notification_ForRateLimitWarning_CreatesWithWarningType()
    {
        // Act
        var notification = new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.RateLimitApproaching,
            NotificationSeverity.Warning,
            "Rate Limit Warning",
            "You are approaching your monthly rate limit");

        // Assert
        notification.Type.Should().Be(NotificationType.RateLimitApproaching);
        notification.Severity.Should().Be(NotificationSeverity.Warning);
    }

    [Fact]
    public void Notification_ForBadgeEarned_CreatesWithInfoSeverity()
    {
        // Act
        var notification = new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.BadgeEarned,
            NotificationSeverity.Info,
            "Badge Earned",
            "Congratulations! You've earned the 'First Upload' badge",
            "/profile/badges",
            "{\"badgeId\":\"first-upload\"}");

        // Assert
        notification.Type.Should().Be(NotificationType.BadgeEarned);
        notification.Severity.Should().Be(NotificationSeverity.Info);
        notification.Link.Should().Be("/profile/badges");
        notification.Metadata.Should().Contain("badgeId");
    }

    [Fact]
    public void Notification_ForAdminNotification_CreatesWithCorrectType()
    {
        // Act
        var notification = new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.AdminNewShareRequest,
            NotificationSeverity.Info,
            "New Share Request",
            "A new game share request requires review",
            "/admin/share-requests",
            "{\"requestId\":\"abc123\"}");

        // Assert
        notification.Type.Should().Be(NotificationType.AdminNewShareRequest);
        notification.Link.Should().Be("/admin/share-requests");
    }

    [Fact]
    public void Notification_ForLoanReminder_CreatesWithWarningType()
    {
        // Act
        var notification = new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.LoanReminder,
            NotificationSeverity.Warning,
            "Loan Reminder",
            "Your game loan is due back in 2 days",
            "/library/loans",
            "{\"loanId\":\"loan123\",\"daysRemaining\":2}");

        // Assert
        notification.Type.Should().Be(NotificationType.LoanReminder);
        notification.Metadata.Should().Contain("daysRemaining");
    }

    #endregion

    #region Read/Unread Workflow Tests

    [Fact]
    public async Task Notification_FullReadUnreadCycle_WorksCorrectly()
    {
        // Arrange
        var notification = CreateTestNotification();

        // Initial state
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();

        // Mark as read
        notification.MarkAsRead();
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
        var firstReadAt = notification.ReadAt;

        // Mark as unread
        notification.MarkAsUnread();
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();

        // Mark as read again
        await Task.Delay(50);
        notification.MarkAsRead();
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
        notification.ReadAt.Should().BeAfter(firstReadAt!.Value);
    }

    [Fact]
    public void Notification_RestoreThenMarkAsUnread_WorksCorrectly()
    {
        // Arrange
        var notification = CreateTestNotification();
        var historicalReadTime = DateTime.UtcNow.AddDays(-5);

        // Act
        notification.RestoreReadStatus(historicalReadTime);
        notification.IsRead.Should().BeTrue();

        notification.MarkAsUnread();

        // Assert
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static Notification CreateTestNotification()
    {
        return new Notification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationType.PdfUploadCompleted,
            NotificationSeverity.Success,
            "Test Notification",
            "This is a test notification message");
    }

    #endregion
}
