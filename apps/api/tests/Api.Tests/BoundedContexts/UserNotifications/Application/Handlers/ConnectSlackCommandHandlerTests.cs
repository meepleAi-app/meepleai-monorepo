using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.Tests.Constants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class ConnectSlackCommandHandlerTests
{
    private readonly Mock<IOptions<SlackNotificationConfiguration>> _configMock;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly Mock<ILogger<ConnectSlackCommandHandler>> _loggerMock;
    private readonly ConnectSlackCommandHandler _handler;
    private readonly SlackNotificationConfiguration _config;

    public ConnectSlackCommandHandlerTests()
    {
        _config = new SlackNotificationConfiguration
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            RedirectUri = "https://app.meepleai.com/api/v1/integrations/slack/callback"
        };
        _configMock = new Mock<IOptions<SlackNotificationConfiguration>>();
        _configMock.Setup(c => c.Value).Returns(_config);
        _dataProtectionProvider = DataProtectionProvider.Create("MeepleAI-Tests");
        _loggerMock = new Mock<ILogger<ConnectSlackCommandHandler>>();
        _handler = new ConnectSlackCommandHandler(_configMock.Object, _dataProtectionProvider, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsValidOAuthUrl_WithClientIdAndRedirectUri()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConnectSlackCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Contain("https://slack.com/oauth/v2/authorize");
        result.Should().Contain("client_id=test-client-id");
        result.Should().Contain("scope=chat:write,im:write");
        result.Should().Contain("state="); // State is now encrypted, not raw userId
        result.Should().Contain("redirect_uri=");
    }

    [Fact]
    public async Task Handle_IncludesRequiredScopes()
    {
        // Arrange
        var command = new ConnectSlackCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - scope contains both required Slack scopes
        result.Should().Contain("scope=chat:write,im:write");
    }

    [Fact]
    public async Task Handle_GeneratesSignedStateToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ConnectSlackCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — state is not the raw userId; it's an encrypted token
        result.Should().NotContain($"state={userId}");
        result.Should().Contain("state=");

        // Verify the state can be decrypted back to the userId
        var uri = new Uri(result);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var stateToken = query["state"]!;
        var protector = _dataProtectionProvider.CreateProtector("MeepleAI.SlackOAuth");
        var timedProtector = protector.ToTimeLimitedDataProtector();
        var decryptedUserId = timedProtector.Unprotect(stateToken);
        decryptedUserId.Should().Be(userId.ToString());
    }
}
