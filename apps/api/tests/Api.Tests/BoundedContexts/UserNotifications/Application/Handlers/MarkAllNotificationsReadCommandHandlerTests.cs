using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Tests for MarkAllNotificationsReadCommandHandler.
/// Bulk operation that uses ExecuteUpdateAsync (no IUnitOfWork needed).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class MarkAllNotificationsReadCommandHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<ILogger<MarkAllNotificationsReadCommandHandler>> _loggerMock;
    private readonly MarkAllNotificationsReadCommandHandler _handler;

    public MarkAllNotificationsReadCommandHandlerTests()
    {
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _loggerMock = new Mock<ILogger<MarkAllNotificationsReadCommandHandler>>();
        _handler = new MarkAllNotificationsReadCommandHandler(
            _notificationRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithUnreadNotifications_ReturnsMarkedCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _notificationRepositoryMock
            .Setup(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var command = new MarkAllNotificationsReadCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(5);
        _notificationRepositoryMock.Verify(
            r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoUnreadNotifications_ReturnsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _notificationRepositoryMock
            .Setup(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var command = new MarkAllNotificationsReadCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _notificationRepositoryMock
            .Setup(r => r.MarkAllAsReadAsync(userId, cancellationToken))
            .ReturnsAsync(3);

        var command = new MarkAllNotificationsReadCommand(userId);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _notificationRepositoryMock.Verify(
            r => r.MarkAllAsReadAsync(userId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithManyNotifications_ReturnsCorrectCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _notificationRepositoryMock
            .Setup(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var command = new MarkAllNotificationsReadCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(100);
    }
}
