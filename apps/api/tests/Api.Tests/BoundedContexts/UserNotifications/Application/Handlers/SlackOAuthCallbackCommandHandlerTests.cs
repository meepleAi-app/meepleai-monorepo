using System.Net;
using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class SlackOAuthCallbackCommandHandlerTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ISlackConnectionRepository> _connectionRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IOptions<SlackNotificationConfiguration>> _configMock;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly Mock<ILogger<SlackOAuthCallbackCommandHandler>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly SlackOAuthCallbackCommandHandler _handler;

    public SlackOAuthCallbackCommandHandlerTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _connectionRepoMock = new Mock<ISlackConnectionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _dataProtectionProvider = DataProtectionProvider.Create("MeepleAI-Tests");
        _loggerMock = new Mock<ILogger<SlackOAuthCallbackCommandHandler>>();

        var config = new SlackNotificationConfiguration
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            RedirectUri = "https://app.meepleai.com/callback"
        };
        _configMock = new Mock<IOptions<SlackNotificationConfiguration>>();
        _configMock.Setup(c => c.Value).Returns(config);

        _httpHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("SlackOAuth")).Returns(httpClient);

        _handler = new SlackOAuthCallbackCommandHandler(
            _httpClientFactoryMock.Object,
            _connectionRepoMock.Object,
            _unitOfWorkMock.Object,
            _configMock.Object,
            _dataProtectionProvider,
            _loggerMock.Object);
    }

    private string CreateSignedState(Guid userId)
    {
        var protector = _dataProtectionProvider.CreateProtector("MeepleAI.SlackOAuth");
        var timedProtector = protector.ToTimeLimitedDataProtector();
        return timedProtector.Protect(userId.ToString(), TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Handle_WithValidCode_CreatesNewConnection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupSuccessfulOAuthFlow();
        _connectionRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlackConnection?)null);

        var command = new SlackOAuthCallbackCommand("valid-code", CreateSignedState(userId));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _connectionRepoMock.Verify(
            r => r.AddAsync(It.Is<SlackConnection>(c =>
                c.UserId == userId &&
                c.SlackUserId == "U123456" &&
                c.SlackTeamId == "T123456" &&
                c.SlackTeamName == "Test Team" &&
                c.DmChannelId == "D987654" &&
                c.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingConnection_ReconnectsInsteadOfCreatingNew()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupSuccessfulOAuthFlow();
        var existing = SlackConnection.Create(userId, "U000", "T000", "Old Team", "old-token", "D000");
        existing.Disconnect(DateTime.UtcNow);
        _connectionRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new SlackOAuthCallbackCommand("valid-code", CreateSignedState(userId));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        existing.IsActive.Should().BeTrue();
        _connectionRepoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _connectionRepoMock.Verify(r => r.AddAsync(It.IsAny<SlackConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidState_ReturnsFalse()
    {
        // Arrange — unsigned/tampered state token
        var command = new SlackOAuthCallbackCommand("valid-code", "not-a-valid-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithFailedTokenExchange_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupFailedTokenExchange();
        var command = new SlackOAuthCallbackCommand("bad-code", CreateSignedState(userId));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _connectionRepoMock.Verify(r => r.AddAsync(It.IsAny<SlackConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupSuccessfulOAuthFlow()
    {
        var tokenResponse = JsonSerializer.Serialize(new
        {
            ok = true,
            access_token = "xoxb-test-token",
            authed_user = new { id = "U123456" },
            team = new { id = "T123456", name = "Test Team" }
        });

        var dmResponse = JsonSerializer.Serialize(new
        {
            ok = true,
            channel = new { id = "D987654" }
        });

        var callCount = 0;
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var content = callCount == 1 ? tokenResponse : dmResponse;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                };
            });
    }

    private void SetupFailedTokenExchange()
    {
        var errorResponse = JsonSerializer.Serialize(new
        {
            ok = false,
            error = "invalid_code"
        });

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(errorResponse, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
