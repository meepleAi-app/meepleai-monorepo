using System.Net;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class DisconnectSlackCommandHandlerTests
{
    private readonly Mock<ISlackConnectionRepository> _connectionRepoMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DisconnectSlackCommandHandler>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly DisconnectSlackCommandHandler _handler;

    public DisconnectSlackCommandHandlerTests()
    {
        _connectionRepoMock = new Mock<ISlackConnectionRepository>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DisconnectSlackCommandHandler>>();

        var config = new SlackNotificationConfiguration
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret"
        };
        var configMock = new Mock<IOptions<SlackNotificationConfiguration>>();
        configMock.Setup(c => c.Value).Returns(config);

        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", System.Text.Encoding.UTF8, "application/json")
            });
        var httpClient = new HttpClient(_httpHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("SlackOAuth")).Returns(httpClient);

        _handler = new DisconnectSlackCommandHandler(
            _connectionRepoMock.Object,
            _httpClientFactoryMock.Object,
            _unitOfWorkMock.Object,
            configMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithActiveConnection_DisconnectsAndSaves()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connection = SlackConnection.Create(userId, "U123", "T123", "Team", "xoxb-token", "D123");
        _connectionRepoMock
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var command = new DisconnectSlackCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        connection.IsActive.Should().BeFalse();
        connection.DisconnectedAt.Should().NotBeNull();
        _connectionRepoMock.Verify(r => r.UpdateAsync(connection, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoActiveConnection_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _connectionRepoMock
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlackConnection?)null);

        var command = new DisconnectSlackCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenRevocationFails_StillDisconnectsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var connection = SlackConnection.Create(userId, "U123", "T123", "Team", "xoxb-token", "D123");
        _connectionRepoMock
            .Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var command = new DisconnectSlackCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - disconnect still succeeds even if revocation fails
        result.Should().BeTrue();
        connection.IsActive.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
