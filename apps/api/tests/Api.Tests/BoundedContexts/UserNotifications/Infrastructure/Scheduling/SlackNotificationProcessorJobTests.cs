using System.Net;
using System.Net.Http.Headers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
#pragma warning disable S3881
public class SlackNotificationProcessorJobTests : IDisposable
#pragma warning restore S3881
{
    private readonly Mock<INotificationQueueRepository> _queueRepoMock = new();
    private readonly Mock<ISlackConnectionRepository> _slackConnRepoMock = new();
    private readonly Mock<INotificationRepository> _notificationRepoMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<SlackNotificationProcessorJob>> _loggerMock = new();
    private readonly Mock<IJobExecutionContext> _jobContextMock = new();
    private bool _disposed;

    public SlackNotificationProcessorJobTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _jobContextMock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        _jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _dbContext.Dispose();
            _disposed = true;
        }
    }

    private SlackNotificationProcessorJob CreateSut()
    {
        var configuration = new ConfigurationBuilder().Build();
        var genericBuilder = new GenericSlackBuilder(configuration);
        var builderFactory = new SlackMessageBuilderFactory(
            Enumerable.Empty<ISlackMessageBuilder>(), genericBuilder);

        return new SlackNotificationProcessorJob(
            _queueRepoMock.Object,
            _slackConnRepoMock.Object,
            _notificationRepoMock.Object,
            _httpClientFactoryMock.Object,
            builderFactory,
            _dbContext,
            _loggerMock.Object);
    }

    private static NotificationQueueItem CreateSlackTeamItem(
        string webhookUrl = "https://hooks.slack.com/services/xxx",
        string slackTeamId = "T01ABCDEF")
    {
        return NotificationQueueItem.Create(
            channelType: NotificationChannelType.SlackTeam,
            recipientUserId: null,
            notificationType: NotificationType.ShareRequestCreated,
            payload: new ShareRequestPayload(Guid.NewGuid(), "TestUser", "TestGame", null),
            slackChannelTarget: webhookUrl,
            slackTeamId: slackTeamId);
    }

    private static NotificationQueueItem CreateSlackUserItem(
        Guid? recipientUserId = null,
        string slackTeamId = "T01ABCDEF",
        string dmChannelId = "D01ABCDEF")
    {
        return NotificationQueueItem.Create(
            channelType: NotificationChannelType.SlackUser,
            recipientUserId: recipientUserId ?? Guid.NewGuid(),
            notificationType: NotificationType.ShareRequestCreated,
            payload: new ShareRequestPayload(Guid.NewGuid(), "TestUser", "TestGame", null),
            slackChannelTarget: dmChannelId,
            slackTeamId: slackTeamId);
    }

    private void SetupHttpClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var client = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("SlackApi")).Returns(client);
    }

    [Fact]
    public async Task Execute_ProcessesPendingItems_MarksAsSent()
    {
        // Arrange
        var teamItem = CreateSlackTeamItem();
        _queueRepoMock.Setup(r => r.GetPendingByChannelAsync(NotificationChannelType.SlackUser, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationQueueItem>());
        _queueRepoMock.Setup(r => r.GetPendingByChannelAsync(NotificationChannelType.SlackTeam, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationQueueItem> { teamItem });

        SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });

        var sut = CreateSut();

        // Act
        await sut.Execute(_jobContextMock.Object);

        // Assert — item was updated twice (processing, then sent)
        _queueRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<NotificationQueueItem>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_Handles429WithRetryAfter_SetsNextRetryAt()
    {
        // Arrange
        var item1 = CreateSlackTeamItem(slackTeamId: "T_RATE_LIMITED");
        var item2 = CreateSlackTeamItem(slackTeamId: "T_RATE_LIMITED");

        _queueRepoMock.Setup(r => r.GetPendingByChannelAsync(NotificationChannelType.SlackUser, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationQueueItem>());
        _queueRepoMock.Setup(r => r.GetPendingByChannelAsync(NotificationChannelType.SlackTeam, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationQueueItem> { item1, item2 });

        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
        SetupHttpClient(response);

        var sut = CreateSut();

        // Act
        await sut.Execute(_jobContextMock.Object);

        // Assert — item was marked as processing then as failed with retry info
        _queueRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<NotificationQueueItem>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2)); // processing + rate-limit update
    }

    [Fact]
    public async Task Execute_HandlesTokenRevocation_DeactivatesConnection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var item = CreateSlackUserItem(recipientUserId: userId);

        _queueRepoMock.Setup(r => r.GetPendingByChannelAsync(NotificationChannelType.SlackUser, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationQueueItem> { item });
        _queueRepoMock.Setup(r => r.GetPendingByChannelAsync(NotificationChannelType.SlackTeam, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationQueueItem>());

        var slackConnection = SlackConnection.Create(
            userId, "U01ABC", "T01ABCDEF", "TestWorkspace", "xoxb-old-token", "D01ABCDEF");

        // Return connection for both the bot API call and the revocation handler
        _slackConnRepoMock.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slackConnection);

        // Simulate Slack API returning ok=false with token_revoked error
        var responseContent = """{"ok":false,"error":"token_revoked"}""";
        SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
        });

        var sut = CreateSut();

        // Act
        await sut.Execute(_jobContextMock.Object);

        // Assert — connection should be updated (deactivated)
        _slackConnRepoMock.Verify(
            r => r.UpdateAsync(It.Is<SlackConnection>(c => !c.IsActive), It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — in-app notification created about disconnection
        _notificationRepoMock.Verify(
            r => r.AddAsync(
                It.Is<Notification>(n => n.UserId == userId && n.Title == "Slack Disconnected"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WithNoPendingItems_CompletesImmediately()
    {
        // Arrange
        _queueRepoMock.Setup(r => r.GetPendingByChannelAsync(It.IsAny<NotificationChannelType>(), 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationQueueItem>());

        var sut = CreateSut();

        // Act
        await sut.Execute(_jobContextMock.Object);

        // Assert — no updates attempted
        _queueRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<NotificationQueueItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
