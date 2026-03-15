using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.Aggregates;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public class NotificationQueueItemTests
{
    private static readonly Guid DefaultRecipientUserId = Guid.NewGuid();

    private static NotificationQueueItem CreateDefaultItem(
        NotificationChannelType? channelType = null,
        Guid? recipientUserId = null,
        bool nullRecipient = false,
        NotificationType? notificationType = null,
        INotificationPayload? payload = null,
        string? slackChannelTarget = null,
        string? slackTeamId = null)
    {
        return NotificationQueueItem.Create(
            channelType ?? NotificationChannelType.SlackUser,
            nullRecipient ? null : (recipientUserId ?? DefaultRecipientUserId),
            notificationType ?? NotificationType.PdfUploadCompleted,
            payload ?? new GenericPayload("Test", "Body"),
            slackChannelTarget,
            slackTeamId);
    }

    [Fact]
    public void Create_SetsCorrectDefaults()
    {
        // Arrange & Act
        var item = CreateDefaultItem();

        // Assert
        Assert.Equal(NotificationQueueStatus.Pending, item.Status);
        Assert.Equal(0, item.RetryCount);
        Assert.Equal(3, item.MaxRetries);
        Assert.Null(item.NextRetryAt);
        Assert.Null(item.LastError);
        Assert.Null(item.ProcessedAt);
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.NotEqual(Guid.Empty, item.CorrelationId);
    }

    [Fact]
    public void Create_ForSlackTeam_AllowsNullRecipientUserId()
    {
        // Arrange & Act
        var item = CreateDefaultItem(
            channelType: NotificationChannelType.SlackTeam,
            nullRecipient: true,
            slackChannelTarget: "https://hooks.slack.com/services/xxx",
            slackTeamId: "T12345");

        // Assert
        Assert.Equal(NotificationChannelType.SlackTeam, item.ChannelType);
        Assert.Null(item.RecipientUserId);
        Assert.Equal("https://hooks.slack.com/services/xxx", item.SlackChannelTarget);
        Assert.Equal("T12345", item.SlackTeamId);
    }

    [Fact]
    public void Create_ThrowsOnNullChannelType()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NotificationQueueItem.Create(
                null!,
                Guid.NewGuid(),
                NotificationType.PdfUploadCompleted,
                new GenericPayload("Test", "Body")));
    }

    [Fact]
    public void Create_ThrowsOnNullNotificationType()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NotificationQueueItem.Create(
                NotificationChannelType.SlackUser,
                Guid.NewGuid(),
                null!,
                new GenericPayload("Test", "Body")));
    }

    [Fact]
    public void Create_ThrowsOnNullPayload()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NotificationQueueItem.Create(
                NotificationChannelType.SlackUser,
                Guid.NewGuid(),
                NotificationType.PdfUploadCompleted,
                null!));
    }

    [Fact]
    public void MarkAsProcessing_FromPending_ChangesStatus()
    {
        // Arrange
        var item = CreateDefaultItem();

        // Act
        item.MarkAsProcessing();

        // Assert
        Assert.Equal(NotificationQueueStatus.Processing, item.Status);
    }

    [Fact]
    public void MarkAsProcessing_FromFailed_ChangesStatus()
    {
        // Arrange
        var item = CreateDefaultItem();
        item.MarkAsProcessing();
        item.MarkAsFailed("transient error");

        // Act
        item.MarkAsProcessing();

        // Assert
        Assert.Equal(NotificationQueueStatus.Processing, item.Status);
    }

    [Fact]
    public void MarkAsProcessing_FromSent_Throws()
    {
        // Arrange
        var item = CreateDefaultItem();
        item.MarkAsProcessing();
        item.MarkAsSent(DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => item.MarkAsProcessing());
    }

    [Fact]
    public void MarkAsSent_SetsStatusAndProcessedAt()
    {
        // Arrange
        var item = CreateDefaultItem();
        item.MarkAsProcessing();
        var processedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        item.MarkAsSent(processedAt);

        // Assert
        Assert.Equal(NotificationQueueStatus.Sent, item.Status);
        Assert.Equal(processedAt, item.ProcessedAt);
        Assert.Null(item.LastError);
    }

    [Fact]
    public void MarkAsSent_FromPending_Throws()
    {
        // Arrange
        var item = CreateDefaultItem();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            item.MarkAsSent(DateTime.UtcNow));
    }

    [Fact]
    public void MarkAsFailed_IncrementsRetryCount_SetsNextRetryAt()
    {
        // Arrange
        var item = CreateDefaultItem();
        item.MarkAsProcessing();
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        item.MarkAsFailed("connection refused", now);

        // Assert
        Assert.Equal(1, item.RetryCount);
        Assert.Equal(NotificationQueueStatus.Failed, item.Status);
        Assert.Equal("connection refused", item.LastError);
        // First failure: +1 minute
        Assert.Equal(now.AddMinutes(1), item.NextRetryAt);
    }

    [Fact]
    public void MarkAsFailed_SecondFailure_FiveMinuteDelay()
    {
        // Arrange
        var item = CreateDefaultItem();
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        // First failure
        item.MarkAsProcessing();
        item.MarkAsFailed("error 1", now);

        // Second failure
        var now2 = now.AddMinutes(2);
        item.MarkAsProcessing();
        item.MarkAsFailed("error 2", now2);

        // Assert
        Assert.Equal(2, item.RetryCount);
        Assert.Equal(NotificationQueueStatus.Failed, item.Status);
        // Second failure: +5 minutes
        Assert.Equal(now2.AddMinutes(5), item.NextRetryAt);
    }

    [Fact]
    public void MarkAsFailed_ThirdFailure_StillRetries()
    {
        // Arrange
        var item = CreateDefaultItem();
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        // First failure
        item.MarkAsProcessing();
        item.MarkAsFailed("error 1", now);

        // Second failure
        item.MarkAsProcessing();
        item.MarkAsFailed("error 2", now.AddMinutes(2));

        // Third failure — RetryCount == MaxRetries (3 == 3), but off-by-one fix means one more retry
        item.MarkAsProcessing();
        var now3 = now.AddMinutes(10);
        item.MarkAsFailed("error 3", now3);

        // Assert — still retrying (4 total attempts: initial + 3 retries matching [1m, 5m, 30m] delays)
        Assert.Equal(3, item.RetryCount);
        Assert.Equal(NotificationQueueStatus.Failed, item.Status);
        // Third failure uses last delay (30 minutes)
        Assert.Equal(now3.AddMinutes(30), item.NextRetryAt);
        Assert.Equal("error 3", item.LastError);
    }

    [Fact]
    public void MarkAsFailed_FourthFailure_DeadLetters()
    {
        // Arrange
        var item = CreateDefaultItem();
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        // First failure
        item.MarkAsProcessing();
        item.MarkAsFailed("error 1", now);

        // Second failure
        item.MarkAsProcessing();
        item.MarkAsFailed("error 2", now.AddMinutes(2));

        // Third failure
        item.MarkAsProcessing();
        item.MarkAsFailed("error 3", now.AddMinutes(10));

        // Fourth failure — RetryCount > MaxRetries (4 > 3), dead-lettered
        item.MarkAsProcessing();
        item.MarkAsFailed("error 4", now.AddMinutes(40));

        // Assert
        Assert.Equal(4, item.RetryCount);
        Assert.Equal(NotificationQueueStatus.DeadLetter, item.Status);
        Assert.Null(item.NextRetryAt);
        Assert.Equal("error 4", item.LastError);
    }

    [Fact]
    public void MarkAsFailed_FromPending_Throws()
    {
        // Arrange
        var item = CreateDefaultItem();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            item.MarkAsFailed("error"));
    }

    [Fact]
    public void MarkAsDeadLetter_SetsStatusAndClearsNextRetry()
    {
        // Arrange
        var item = CreateDefaultItem();
        item.MarkAsProcessing();

        // Act
        item.MarkAsDeadLetter("permanently undeliverable");

        // Assert
        Assert.Equal(NotificationQueueStatus.DeadLetter, item.Status);
        Assert.Equal("permanently undeliverable", item.LastError);
        Assert.Null(item.NextRetryAt);
    }

    [Fact]
    public void SetNextRetryAt_SetsField()
    {
        // Arrange
        var item = CreateDefaultItem();
        var retryAt = new DateTime(2026, 3, 15, 13, 0, 0, DateTimeKind.Utc);

        // Act
        item.SetNextRetryAt(retryAt);

        // Assert
        Assert.Equal(retryAt, item.NextRetryAt);
    }

    [Fact]
    public void Create_SetsChannelTypeAndPayload()
    {
        // Arrange
        var payload = new PdfProcessingPayload(Guid.NewGuid(), "rules.pdf", "completed");

        // Act
        var item = CreateDefaultItem(
            channelType: NotificationChannelType.SlackUser,
            notificationType: NotificationType.PdfUploadCompleted,
            payload: payload);

        // Assert
        Assert.Equal(NotificationChannelType.SlackUser, item.ChannelType);
        Assert.Equal(NotificationType.PdfUploadCompleted, item.NotificationType);
        Assert.IsType<PdfProcessingPayload>(item.Payload);
    }
}
