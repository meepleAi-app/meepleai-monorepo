using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Handlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class UnsubscribePushNotificationsCommandHandlerTests
{
    private readonly Mock<INotificationPreferencesRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UnsubscribePushNotificationsCommandHandler>> _loggerMock;
    private readonly UnsubscribePushNotificationsCommandHandler _handler;

    public UnsubscribePushNotificationsCommandHandlerTests()
    {
        _repositoryMock = new Mock<INotificationPreferencesRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UnsubscribePushNotificationsCommandHandler>>();
        _handler = new UnsubscribePushNotificationsCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingSubscription_ClearsSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var prefs = NotificationPreferences.Reconstitute(
            Guid.NewGuid(), userId,
            true, true, false,
            true, true, false,
            true, true, true,
            "https://push.example.com/endpoint", "p256dhkey", "authkey");

        Assert.True(prefs.HasPushSubscription);

        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var command = new UnsubscribePushNotificationsCommand(userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Null(prefs.PushEndpoint);
        Assert.Null(prefs.PushP256dhKey);
        Assert.Null(prefs.PushAuthKey);
        Assert.False(prefs.HasPushSubscription);

        _repositoryMock.Verify(r => r.UpdateAsync(prefs, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoExistingPreferences_DoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var command = new UnsubscribePushNotificationsCommand(userId);

        // Act - Should not throw
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<NotificationPreferences>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
