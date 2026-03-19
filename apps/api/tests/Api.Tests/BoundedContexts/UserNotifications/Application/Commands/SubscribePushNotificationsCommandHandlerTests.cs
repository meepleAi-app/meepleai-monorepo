using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class SubscribePushNotificationsCommandHandlerTests
{
    private readonly Mock<INotificationPreferencesRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<SubscribePushNotificationsCommandHandler>> _loggerMock;
    private readonly SubscribePushNotificationsCommandHandler _handler;

    public SubscribePushNotificationsCommandHandlerTests()
    {
        _repositoryMock = new Mock<INotificationPreferencesRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<SubscribePushNotificationsCommandHandler>>();
        _handler = new SubscribePushNotificationsCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingPreferences_UpdatesSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingPrefs = new NotificationPreferences(userId);
        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPrefs);

        var command = new SubscribePushNotificationsCommand(
            userId, "https://push.example.com/endpoint", "p256dhkey123", "authkey456");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("https://push.example.com/endpoint", existingPrefs.PushEndpoint);
        Assert.Equal("p256dhkey123", existingPrefs.PushP256dhKey);
        Assert.Equal("authkey456", existingPrefs.PushAuthKey);
        Assert.True(existingPrefs.HasPushSubscription);

        _repositoryMock.Verify(r => r.UpdateAsync(existingPrefs, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoExistingPreferences_CreatesNewAndSubscribes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var command = new SubscribePushNotificationsCommand(
            userId, "https://push.example.com/endpoint", "p256dhkey123", "authkey456");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(
            r => r.AddAsync(It.Is<NotificationPreferences>(p =>
                p.PushEndpoint == "https://push.example.com/endpoint" &&
                p.PushP256dhKey == "p256dhkey123" &&
                p.PushAuthKey == "authkey456" &&
                p.HasPushSubscription),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
