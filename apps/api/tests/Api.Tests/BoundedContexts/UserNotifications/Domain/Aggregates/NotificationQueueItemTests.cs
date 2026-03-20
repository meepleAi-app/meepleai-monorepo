using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Xunit;
using FluentAssertions;

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
        item.Status.Should().Be(NotificationQueueStatus.Pending);
        item.RetryCount.Should().Be(0);
        item.MaxRetries.Should().Be(3);
        item.NextRetryAt.Should().BeNull();
        item.LastError.Should().BeNull();
        item.ProcessedAt.Should().BeNull();
        item.Id.Should().NotBe(Guid.Empty);
        item.CorrelationId.Should().NotBe(Guid.Empty);
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
        item.ChannelType.Should().Be(NotificationChannelType.SlackTeam);
        item.RecipientUserId.Should().BeNull();
        item.SlackChannelTarget.Should().Be("https://hooks.slack.com/services/xxx");
        item.SlackTeamId.Should().Be("T12345");
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
        item.Status.Should().Be(NotificationQueueStatus.Processing);
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
        item.Status.Should().Be(NotificationQueueStatus.Processing);
    }

    [Fact]
    public void MarkAsProcessing_FromSent_Throws()
    {
        // Arrange
        var item = CreateDefaultItem();
        item.MarkAsProcessing();
        item.MarkAsSent(DateTime.UtcNow);

        // Act & Assert
        ((Action)(() => item.MarkAsProcessing())).Should().Throw<InvalidOperationException>();
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
        item.Status.Should().Be(NotificationQueueStatus.Sent);
        item.ProcessedAt.Should().Be(processedAt);
        item.LastError.Should().BeNull();
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
        item.RetryCount.Should().Be(1);
        item.Status.Should().Be(NotificationQueueStatus.Failed);
        item.LastError.Should().Be("connection refused");
        // First failure: +1 minute
        item.NextRetryAt.Should().Be(now.AddMinutes(1));
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
        item.RetryCount.Should().Be(2);
        item.Status.Should().Be(NotificationQueueStatus.Failed);
        // Second failure: +5 minutes
        item.NextRetryAt.Should().Be(now2.AddMinutes(5));
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
        item.RetryCount.Should().Be(3);
        item.Status.Should().Be(NotificationQueueStatus.Failed);
        // Third failure uses last delay (30 minutes)
        item.NextRetryAt.Should().Be(now3.AddMinutes(30));
        item.LastError.Should().Be("error 3");
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
        item.RetryCount.Should().Be(4);
        item.Status.Should().Be(NotificationQueueStatus.DeadLetter);
        item.NextRetryAt.Should().BeNull();
        item.LastError.Should().Be("error 4");
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
        item.Status.Should().Be(NotificationQueueStatus.DeadLetter);
        item.LastError.Should().Be("permanently undeliverable");
        item.NextRetryAt.Should().BeNull();
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
        item.NextRetryAt.Should().Be(retryAt);
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
        item.ChannelType.Should().Be(NotificationChannelType.SlackUser);
        item.NotificationType.Should().Be(NotificationType.PdfUploadCompleted);
        item.Payload.Should().BeOfType<PdfProcessingPayload>();
    }
}
