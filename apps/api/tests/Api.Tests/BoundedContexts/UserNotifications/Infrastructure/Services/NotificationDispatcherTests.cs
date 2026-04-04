using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public class NotificationDispatcherTests
{
    private readonly Mock<INotificationRepository> _notificationRepoMock = new();
    private readonly Mock<INotificationQueueRepository> _queueRepoMock = new();
    private readonly Mock<INotificationPreferencesRepository> _prefsRepoMock = new();
    private readonly Mock<ISlackConnectionRepository> _slackConnRepoMock = new();
    private readonly Mock<ILogger<NotificationDispatcher>> _loggerMock = new();
    private readonly SlackNotificationConfiguration _slackConfig = new();

    private NotificationDispatcher CreateSut()
    {
        return new NotificationDispatcher(
            _notificationRepoMock.Object,
            _queueRepoMock.Object,
            _prefsRepoMock.Object,
            _slackConnRepoMock.Object,
            Options.Create(_slackConfig),
            _loggerMock.Object);
    }

    private static NotificationMessage CreateMessage(
        NotificationType? type = null,
        Guid? recipientUserId = null,
        INotificationPayload? payload = null)
    {
        return new NotificationMessage
        {
            Type = type ?? NotificationType.ShareRequestCreated,
            RecipientUserId = recipientUserId ?? Guid.NewGuid(),
            Payload = payload ?? new ShareRequestPayload(Guid.NewGuid(), "TestUser", "TestGame", null),
            DeepLinkPath = "/test/path"
        };
    }

    [Fact]
    public async Task DispatchAsync_AlwaysCreatesInAppNotification()
    {
        // Arrange
        var message = CreateMessage();
        var sut = CreateSut();

        // Act
        await sut.DispatchAsync(message);

        // Assert
        _notificationRepoMock.Verify(
            r => r.AddAsync(It.Is<Notification>(n =>
                n.UserId == message.RecipientUserId &&
                n.Type == message.Type &&
                n.Link == "/test/path"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithEmailEnabled_CreatesEmailQueueItem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = CreateMessage(
            type: NotificationType.ShareRequestCreated,
            recipientUserId: userId);

        var prefs = new NotificationPreferences(userId);
        _prefsRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var sut = CreateSut();

        // Act
        await sut.DispatchAsync(message);

        // Assert — email is enabled by default for ShareRequestCreated (not in the explicit list, defaults to true)
        _queueRepoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<NotificationQueueItem>>(items =>
                    items.Any(i => i.ChannelType == NotificationChannelType.Email)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithActiveSlackConnection_CreatesSlackUserQueueItem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = CreateMessage(
            type: NotificationType.ShareRequestCreated,
            recipientUserId: userId);

        var prefs = new NotificationPreferences(userId);
        _prefsRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var slackConnection = SlackConnection.Create(
            userId, "U01ABC", "T01ABC", "TestWorkspace", "xoxb-token", "D01ABC");
        _slackConnRepoMock.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slackConnection);

        var sut = CreateSut();

        // Act
        await sut.DispatchAsync(message);

        // Assert
        _queueRepoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<NotificationQueueItem>>(items =>
                    items.Any(i =>
                        i.ChannelType == NotificationChannelType.SlackUser &&
                        i.SlackChannelTarget == "D01ABC" &&
                        i.SlackTeamId == "T01ABC")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithTeamChannelMatchingType_CreatesSlackTeamQueueItem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = CreateMessage(
            type: NotificationType.ShareRequestCreated,
            recipientUserId: userId);

        _slackConfig.TeamChannels["T_TEAM1"] = new SlackTeamChannelSettings
        {
            WebhookUrl = "https://hooks.slack.com/services/xxx",
            Channel = "#notifications",
            Types = ["share_request_created", "badge_earned"]
        };

        var sut = CreateSut();

        // Act
        await sut.DispatchAsync(message);

        // Assert
        _queueRepoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<NotificationQueueItem>>(items =>
                    items.Any(i =>
                        i.ChannelType == NotificationChannelType.SlackTeam &&
                        i.SlackChannelTarget == "https://hooks.slack.com/services/xxx" &&
                        i.SlackTeamId == "T_TEAM1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithNullPreferences_DefaultsToSendingEmail()
    {
        // Arrange — no preferences returned defaults to sending email (fail-open)
        var message = CreateMessage();
        var sut = CreateSut();

        // Act
        await sut.DispatchAsync(message);

        // Assert — in-app notification always created
        _notificationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Email queue item should be added (null preferences defaults to email enabled)
        _queueRepoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<NotificationQueueItem>>(items =>
                    items.Any(i => i.ChannelType == NotificationChannelType.Email)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithSlackDisabledInPreferences_SkipsSlackChannel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = CreateMessage(
            type: NotificationType.ShareRequestCreated,
            recipientUserId: userId);

        // Create preferences with Slack globally disabled
        var prefs = NotificationPreferences.Reconstitute(
            id: Guid.NewGuid(), userId: userId,
            emailReady: true, emailFailed: true, emailRetry: false,
            pushReady: true, pushFailed: true, pushRetry: false,
            inAppReady: true, inAppFailed: true, inAppRetry: true,
            slackEnabled: false);
        _prefsRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var slackConnection = SlackConnection.Create(
            userId, "U01ABC", "T01ABC", "TestWorkspace", "xoxb-token", "D01ABC");
        _slackConnRepoMock.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slackConnection);

        var sut = CreateSut();

        // Act
        await sut.DispatchAsync(message);

        // Assert — no Slack DM lookup should happen when SlackEnabled=false
        _slackConnRepoMock.Verify(
            r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Only email queue item (no Slack)
        _queueRepoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<NotificationQueueItem>>(items =>
                    items.All(i => i.ChannelType != NotificationChannelType.SlackUser)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithPerTypeSlackDisabled_SkipsSlackForThatType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = CreateMessage(
            type: NotificationType.ShareRequestCreated,
            recipientUserId: userId);

        // Slack enabled globally but ShareRequestCreated disabled
        var prefs = NotificationPreferences.Reconstitute(
            id: Guid.NewGuid(), userId: userId,
            emailReady: true, emailFailed: true, emailRetry: false,
            pushReady: true, pushFailed: true, pushRetry: false,
            inAppReady: true, inAppFailed: true, inAppRetry: true,
            slackEnabled: true, slackOnShareRequestCreated: false);
        _prefsRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var sut = CreateSut();

        // Act
        await sut.DispatchAsync(message);

        // Assert — per-type Slack disabled means connection is never looked up
        _slackConnRepoMock.Verify(
            r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Only email queue item should be created (no Slack)
        _queueRepoMock.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<NotificationQueueItem>>(items =>
                    items.All(i => i.ChannelType != NotificationChannelType.SlackUser)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
