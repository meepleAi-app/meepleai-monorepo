using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Handlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public sealed class SendTestPushNotificationCommandHandlerTests
{
    private readonly Mock<INotificationPreferencesRepository> _repositoryMock;
    private readonly Mock<IPushNotificationService> _pushServiceMock;
    private readonly Mock<ILogger<SendTestPushNotificationCommandHandler>> _loggerMock;
    private readonly SendTestPushNotificationCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();

    public SendTestPushNotificationCommandHandlerTests()
    {
        _repositoryMock = new Mock<INotificationPreferencesRepository>();
        _pushServiceMock = new Mock<IPushNotificationService>();
        _loggerMock = new Mock<ILogger<SendTestPushNotificationCommandHandler>>();
        _handler = new SendTestPushNotificationCommandHandler(
            _repositoryMock.Object,
            _pushServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidSubscription_SendsTestPush()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdatePushSubscription("https://push.example.com/endpoint", "p256dh_key", "auth_key");

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var command = new SendTestPushNotificationCommand(_userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _pushServiceMock.Verify(p => p.SendPushNotificationAsync(
            "https://push.example.com/endpoint",
            "p256dh_key",
            "auth_key",
            "Test Notification",
            "Push notifications are working correctly!",
            "/settings/notifications",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoPreferences_ThrowsInvalidOperation()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var command = new SendTestPushNotificationCommand(_userId);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*push subscription*");
    }

    [Fact]
    public async Task Handle_NoSubscription_ThrowsInvalidOperation()
    {
        // Arrange: prefs exist but no push subscription
        var prefs = new NotificationPreferences(_userId);
        // No call to UpdatePushSubscription → HasPushSubscription = false

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var command = new SendTestPushNotificationCommand(_userId);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*push subscription*");

        _pushServiceMock.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PushServiceThrows_Propagates()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdatePushSubscription("https://push.example.com/endpoint", "p256dh_key", "auth_key");

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        _pushServiceMock
            .Setup(p => p.SendPushNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Push service unreachable"));

        var command = new SendTestPushNotificationCommand(_userId);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
