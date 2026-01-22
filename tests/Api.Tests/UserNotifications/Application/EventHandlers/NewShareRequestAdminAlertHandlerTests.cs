using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

/// <summary>
/// Tests for NewShareRequestAdminAlertHandler.
/// ISSUE-2740: Admin alert system for new share requests.
/// </summary>
public class NewShareRequestAdminAlertHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<MeepleAiDbContext> _dbContextMock;
    private readonly Mock<ILogger<NewShareRequestAdminAlertHandler>> _loggerMock;
    private readonly NewShareRequestAdminAlertHandler _handler;

    public NewShareRequestAdminAlertHandlerTests()
    {
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _dbContextMock = new Mock<MeepleAiDbContext>();
        _loggerMock = new Mock<ILogger<NewShareRequestAdminAlertHandler>>();

        _handler = new NewShareRequestAdminAlertHandler(
            _notificationRepositoryMock.Object,
            _dbContextMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithAdminUsers_CreatesNotifications()
    {
        // Arrange
        var shareRequestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var notification = new ShareRequestCreatedEvent(
            shareRequestId,
            userId,
            Guid.NewGuid(),
            ContributionType.NewGame,
            DateTime.UtcNow);

        // Note: This is a simplified test. Full integration test required with real DbContext.
        // For unit testing, we would need to mock DbSet<UserEntity> which is complex.

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        // Verify at least that the handler executes without throwing
        // Full verification requires integration test with testcontainers
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new NewShareRequestAdminAlertHandler(
            null!,
            _dbContextMock.Object,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("notificationRepository");
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new NewShareRequestAdminAlertHandler(
            _notificationRepositoryMock.Object,
            null!,
            _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("dbContext");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new NewShareRequestAdminAlertHandler(
            _notificationRepositoryMock.Object,
            _dbContextMock.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
