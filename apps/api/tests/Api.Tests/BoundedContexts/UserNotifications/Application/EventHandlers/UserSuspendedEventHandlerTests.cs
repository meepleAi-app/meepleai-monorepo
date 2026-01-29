using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="UserSuspendedEventHandler"/>.
/// Tests email notification sending when user accounts are suspended.
/// Issue #2886: Email notification for user suspension.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public class UserSuspendedEventHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<UserSuspendedEventHandler>> _mockLogger;
    private readonly UserSuspendedEventHandler _handler;
    private bool _disposed;

    public UserSuspendedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<UserSuspendedEventHandler>>();
        _handler = new UserSuspendedEventHandler(
            _dbContext,
            _mockEmailService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidEvent_SendsEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reason = "Violation of community guidelines";
        var @event = new UserSuspendedEvent(userId, reason);

        // Seed user in test database
        var userEntity = new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash123",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            IsSuspended = true,
            SuspendedAt = DateTime.UtcNow,
            SuspendReason = reason
        };
        _dbContext.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync();

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockEmailService.Verify(
            s => s.SendAccountSuspendedEmailAsync(
                "user@example.com",
                "Test User",
                reason,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullDisplayName_UsesEmailAsName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new UserSuspendedEvent(userId, null);

        var userEntity = new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = null,
            PasswordHash = "hash123",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync();

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockEmailService.Verify(
            s => s.SendAccountSuspendedEmailAsync(
                "user@example.com",
                "user@example.com",  // Falls back to email
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_LogsWarningAndDoesNotSendEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new UserSuspendedEvent(userId, "Reason");

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockEmailService.Verify(
            s => s.SendAccountSuspendedEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmailServiceException_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new UserSuspendedEvent(userId, "Reason");

        var userEntity = new UserEntity
        {
            Id = userId,
            Email = "user@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash123",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync();

        _mockEmailService
            .Setup(s => s.SendAccountSuspendedEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        // Act - should not throw
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _dbContext?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
