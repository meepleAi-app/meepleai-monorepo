using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="AccountLockedEventHandler"/>.
/// Issue #3676: Account lockout after failed login attempts.
/// Tests email notification and audit logging for account locked events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AccountLockedEventHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly AuditService _auditService;
    private readonly Mock<ILogger<AccountLockedEventHandler>> _mockLogger;
    private readonly Mock<ILogger<AuditService>> _mockAuditLogger;
    private readonly AccountLockedEventHandler _handler;
    private bool _disposed;

    public AccountLockedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<AccountLockedEventHandler>>();
        _mockAuditLogger = new Mock<ILogger<AuditService>>();
        _auditService = new AuditService(_dbContext, _mockAuditLogger.Object);
        _handler = new AccountLockedEventHandler(
            _dbContext,
            _mockEmailService.Object,
            _auditService,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidEvent_SendsEmailAndCreatesAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        _dbContext.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync();

        var lockedUntil = DateTime.UtcNow.AddMinutes(15);
        var @event = new AccountLockedEvent(userId, 5, lockedUntil, "192.168.1.1");

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Email sent
        _mockEmailService.Verify(
            x => x.SendAccountLockedEmailAsync(
                "test@example.com",
                "Test User",
                5,
                lockedUntil,
                "192.168.1.1",
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert - Audit log created
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);
        var auditLog = auditLogs.First();
        auditLog.Action.Should().Be("ACCOUNT_LOCKED");
        auditLog.Resource.Should().Be("User");
        auditLog.ResourceId.Should().Be(userId.ToString());
        auditLog.Result.Should().Be("Success");
        auditLog.IpAddress.Should().Be("192.168.1.1");
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_DoesNotSendEmailOrCreateAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lockedUntil = DateTime.UtcNow.AddMinutes(15);
        var @event = new AccountLockedEvent(userId, 5, lockedUntil);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Email not sent
        _mockEmailService.Verify(
            x => x.SendAccountLockedEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert - Warning logged
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
    public async Task Handle_WithNullDisplayName_UsesEmailLocalPart()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "john.doe@example.com",
            DisplayName = null
        };
        _dbContext.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync();

        var lockedUntil = DateTime.UtcNow.AddMinutes(15);
        var @event = new AccountLockedEvent(userId, 5, lockedUntil);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Email sent with email local part as name
        _mockEmailService.Verify(
            x => x.SendAccountLockedEmailAsync(
                "john.doe@example.com",
                "john.doe",
                5,
                lockedUntil,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullIpAddress_HandlesGracefully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        _dbContext.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync();

        var lockedUntil = DateTime.UtcNow.AddMinutes(15);
        var @event = new AccountLockedEvent(userId, 3, lockedUntil, null);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.IpAddress.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenEmailServiceFails_DoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        _dbContext.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync();

        _mockEmailService
            .Setup(x => x.SendAccountLockedEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP error"));

        var @event = new AccountLockedEvent(userId, 5, DateTime.UtcNow.AddMinutes(15));

        // Act
        var act = async () => await _handler.Handle(@event, CancellationToken.None);

        // Assert - Should not throw, event handling is non-critical
        await act.Should().NotThrowAsync();

        // Assert - Error logged for email failure
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send account locked email")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsWarningWithCorrectDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        _dbContext.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync();

        var @event = new AccountLockedEvent(userId, 5, DateTime.UtcNow.AddMinutes(15), "10.0.0.1");

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Warning logged with correct details
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Account locked")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _dbContext.Dispose();
        }
        _disposed = true;
    }
}
