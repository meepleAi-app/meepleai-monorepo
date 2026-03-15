using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Handlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class HandleSlackInteractionCommandHandlerTests
{
    private const string SigningSecret = "test-signing-secret";

    private readonly SlackSignatureValidator _signatureValidator;
    private readonly Mock<ISlackConnectionRepository> _slackConnectionRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<HandleSlackInteractionCommandHandler>> _loggerMock;
    private readonly HandleSlackInteractionCommandHandler _handler;
    private readonly FakeTimeProvider _timeProvider;

    public HandleSlackInteractionCommandHandlerTests()
    {
        var configMock = new Mock<IOptions<SlackNotificationConfiguration>>();
        configMock.Setup(c => c.Value).Returns(new SlackNotificationConfiguration
        {
            SigningSecret = SigningSecret
        });
        _signatureValidator = new SlackSignatureValidator(configMock.Object);

        _slackConnectionRepoMock = new Mock<ISlackConnectionRepository>();
        _mediatorMock = new Mock<IMediator>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<HandleSlackInteractionCommandHandler>>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        _handler = new HandleSlackInteractionCommandHandler(
            _signatureValidator,
            _slackConnectionRepoMock.Object,
            _mediatorMock.Object,
            _httpClientFactoryMock.Object,
            _loggerMock.Object,
            _timeProvider);
    }

    [Fact]
    public async Task InvalidSignature_ReturnsFailure()
    {
        // Arrange
        var payload = BuildPayload("share_request_approve", Guid.NewGuid().ToString(), "sr", Guid.NewGuid().ToString());
        var command = new HandleSlackInteractionCommand(payload, "1234567890", "v0=invalid_signature");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid signature", result.ResponseMessage);
        _mediatorMock.Verify(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExpiredBlockId_ReturnsExpiryMessage()
    {
        // Arrange — block_id timestamp is 25 hours ago
        var expiredTimestamp = DateTimeOffset.UtcNow.AddHours(-25).ToUnixTimeSeconds();
        var resourceId = Guid.NewGuid();
        var payload = BuildPayload("share_request_approve", resourceId.ToString(), "sr", resourceId.ToString(), expiredTimestamp);
        var (timestamp, signature) = SignPayload(payload);

        SetupActiveSlackConnection();

        var command = new HandleSlackInteractionCommand(payload, timestamp, signature);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("expired", result.ResponseMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedBlockId_ReturnsInvalidMessage()
    {
        // Arrange — block_id with only 2 segments (missing timestamp)
        var payload = BuildPayloadWithCustomBlockId("share_request_approve", Guid.NewGuid().ToString(), "sr:only-two");
        var (timestamp, signature) = SignPayload(payload);

        var command = new HandleSlackInteractionCommand(payload, timestamp, signature);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid action", result.ResponseMessage!);
    }

    [Fact]
    public async Task NonNumericTimestamp_ReturnsInvalidMessage()
    {
        // Arrange — block_id with non-numeric timestamp
        var resourceId = Guid.NewGuid();
        var payload = BuildPayloadWithCustomBlockId("share_request_approve", resourceId.ToString(), $"sr:{resourceId}:not-a-number");
        var (timestamp, signature) = SignPayload(payload);

        var command = new HandleSlackInteractionCommand(payload, timestamp, signature);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid action", result.ResponseMessage!);
    }

    [Fact]
    public async Task ValidAction_DispatchesCommand()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var blockTimestamp = now.AddMinutes(-5).ToUnixTimeSeconds();

        var payload = BuildPayload("share_request_approve", resourceId.ToString(), "sr", resourceId.ToString(), blockTimestamp);
        var (timestamp, signature) = SignPayload(payload);

        SetupActiveSlackConnection(userId: userId);

        // Capture the dispatched command via Send(object, ct) overload used by MediatR
        IBaseRequest? capturedCommand = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => capturedCommand = cmd as IBaseRequest)
            .ReturnsAsync(new ApproveShareRequestResponse(resourceId, default, null, DateTime.UtcNow));

        var command = new HandleSlackInteractionCommand(payload, timestamp, signature);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("approved", result.ResponseMessage!, StringComparison.OrdinalIgnoreCase);

        var dispatched = Assert.IsType<ApproveShareRequestCommand>(capturedCommand);
        Assert.Equal(resourceId, dispatched.ShareRequestId);
        Assert.Equal(userId, dispatched.AdminId);
    }

    [Fact]
    public async Task UnknownActionId_ReturnsUnknownAction()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        var blockTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var payload = BuildPayload("totally_unknown_action", resourceId.ToString(), "xx", resourceId.ToString(), blockTimestamp);
        var (timestamp, signature) = SignPayload(payload);

        SetupActiveSlackConnection();

        var command = new HandleSlackInteractionCommand(payload, timestamp, signature);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Unknown action", result.ResponseMessage);
    }

    #region Helpers

    private SlackConnection SetupActiveSlackConnection(
        Guid? userId = null,
        string slackUserId = "U01TESTUSER")
    {
        var connection = SlackConnection.Reconstitute(
            id: Guid.NewGuid(),
            userId: userId ?? Guid.NewGuid(),
            slackUserId: slackUserId,
            slackTeamId: "T01TESTTEAM",
            slackTeamName: "Test Workspace",
            botAccessToken: "xoxb-test-token",
            dmChannelId: "D01TESTDM",
            isActive: true,
            connectedAt: DateTime.UtcNow.AddDays(-7),
            disconnectedAt: null);

        _slackConnectionRepoMock
            .Setup(r => r.GetBySlackUserIdAsync(slackUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        return connection;
    }

    private static string BuildPayload(
        string actionId,
        string actionValue,
        string blockPrefix,
        string resourceGuid,
        long? blockTimestamp = null)
    {
        var ts = blockTimestamp ?? DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var blockId = $"{blockPrefix}:{resourceGuid}:{ts}";

        return BuildPayloadWithCustomBlockId(actionId, actionValue, blockId);
    }

    private static string BuildPayloadWithCustomBlockId(
        string actionId,
        string actionValue,
        string blockId)
    {
        var payload = new
        {
            type = "block_actions",
            user = new { id = "U01TESTUSER", name = "testuser" },
            actions = new[]
            {
                new
                {
                    action_id = actionId,
                    value = actionValue,
                    block_id = blockId,
                    type = "button"
                }
            },
            response_url = "https://hooks.slack.com/actions/T01TEST/12345/response"
        };

        return JsonSerializer.Serialize(payload);
    }

    private static (string Timestamp, string Signature) SignPayload(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var baseString = $"v0:{timestamp}:{payload}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(SigningSecret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(baseString));
        var signature = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();
        return (timestamp, signature);
    }

    #endregion

    /// <summary>
    /// Simple fake TimeProvider for testing time-dependent logic.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
