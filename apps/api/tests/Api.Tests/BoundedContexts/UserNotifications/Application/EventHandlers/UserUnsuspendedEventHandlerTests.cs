using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
#pragma warning disable S3881
public class UserUnsuspendedEventHandlerTests : IDisposable
#pragma warning restore S3881
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<INotificationDispatcher> _mockDispatcher;
    private readonly Mock<ILogger<UserUnsuspendedEventHandler>> _mockLogger;
    private readonly UserUnsuspendedEventHandler _handler;
    private bool _disposed;

    public UserUnsuspendedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockDispatcher = new Mock<INotificationDispatcher>();
        _mockLogger = new Mock<ILogger<UserUnsuspendedEventHandler>>();
        _handler = new UserUnsuspendedEventHandler(
            _mockDispatcher.Object,
            _dbContext,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidEvent_DispatchesNotification()
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash123",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            IsSuspended = false
        });
        await _dbContext.SaveChangesAsync();

        await _handler.Handle(new UserUnsuspendedEvent(userId), CancellationToken.None);

        _mockDispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m => m.RecipientUserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_DoesNotDispatch()
    {
        await _handler.Handle(new UserUnsuspendedEvent(Guid.NewGuid()), CancellationToken.None);

        _mockDispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithDispatcherException_DoesNotThrow()
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash123",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _mockDispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("dispatch failure"));

        await _handler.Handle(new UserUnsuspendedEvent(userId), CancellationToken.None);
    }

    public void Dispose()
    {
        if (!_disposed) { _dbContext?.Dispose(); _disposed = true; }
        GC.SuppressFinalize(this);
    }
}
