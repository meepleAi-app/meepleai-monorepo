using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.UserNotifications.TestHelpers;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Tests for MarkNotificationReadCommandHandler.
/// Verifies ownership checks, read marking, and persistence.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class MarkNotificationReadCommandHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<MarkNotificationReadCommandHandler>> _loggerMock;
    private readonly MarkNotificationReadCommandHandler _handler;

    public MarkNotificationReadCommandHandlerTests()
    {
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<MarkNotificationReadCommandHandler>>();
        _handler = new MarkNotificationReadCommandHandler(
            _notificationRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidNotification_MarksAsReadAndReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var notification = new NotificationBuilder()
            .WithId(notificationId)
            .WithUserId(userId)
            .Build();

        _notificationRepositoryMock
            .Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var command = new MarkNotificationReadCommand(notificationId, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);

        _notificationRepositoryMock.Verify(
            r => r.UpdateAsync(notification, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentNotification_ReturnsFalse()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _notificationRepositoryMock
            .Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        var command = new MarkNotificationReadCommand(notificationId, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);

        _notificationRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithDifferentUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        var notification = new NotificationBuilder()
            .WithId(notificationId)
            .WithUserId(ownerId) // Owned by different user
            .Build();

        _notificationRepositoryMock
            .Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var command = new MarkNotificationReadCommand(notificationId, differentUserId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        _notificationRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithAlreadyReadNotification_StillUpdatesAndReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var notification = new NotificationBuilder()
            .WithId(notificationId)
            .WithUserId(userId)
            .AsRead() // Already read
            .Build();

        _notificationRepositoryMock
            .Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var command = new MarkNotificationReadCommand(notificationId, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.True(notification.IsRead);

        // Still calls update even if already read (idempotent behavior)
        _notificationRepositoryMock.Verify(
            r => r.UpdateAsync(notification, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var notification = new NotificationBuilder()
            .WithId(notificationId)
            .WithUserId(userId)
            .Build();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _notificationRepositoryMock
            .Setup(r => r.GetByIdAsync(notificationId, cancellationToken))
            .ReturnsAsync(notification);

        var command = new MarkNotificationReadCommand(notificationId, userId);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _notificationRepositoryMock.Verify(
            r => r.GetByIdAsync(notificationId, cancellationToken),
            Times.Once);
        _notificationRepositoryMock.Verify(
            r => r.UpdateAsync(notification, cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}
