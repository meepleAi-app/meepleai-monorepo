using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="TwoFactorDisabledEventHandler"/>.
/// Tests audit logging and email notification for 2FA disablement events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TwoFactorDisabledEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<TwoFactorDisabledEventHandler>> _mockLogger;
    private readonly TwoFactorDisabledEventHandler _handler;
    private bool _disposed;

    public TwoFactorDisabledEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<TwoFactorDisabledEventHandler>>();
        _handler = new TwoFactorDisabledEventHandler(
            _dbContext,
            _mockUserRepository.Object,
            _mockEmailService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidUser_CreatesAuditLogAndSendsEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new TwoFactorDisabledEvent(userId, wasAdminOverride: false);

        var testUser = new UserBuilder()
            .WithId(userId)
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testUser);

        _mockEmailService
            .Setup(e => e.SendTwoFactorDisabledEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(TwoFactorDisabledEvent));
        auditLog.Details.Should().Contain("TwoFactorDisabled");

        _mockEmailService.Verify(
            e => e.SendTwoFactorDisabledEmailAsync(
                "test@example.com",
                "Test User",
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAdminOverride_SendsEmailWithAdminOverrideFlag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new TwoFactorDisabledEvent(userId, wasAdminOverride: true);

        var testUser = new UserBuilder()
            .WithId(userId)
            .WithEmail("user@test.com")
            .WithDisplayName("Admin Victim")
            .Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testUser);

        _mockEmailService
            .Setup(e => e.SendTwoFactorDisabledEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockEmailService.Verify(
            e => e.SendTwoFactorDisabledEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                true, // wasAdminOverride should be true
                It.IsAny<CancellationToken>()),
            Times.Once);

        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("WasAdminOverride");
        auditLog.Details.Should().Contain("true");
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_CreatesAuditLogButNoEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new TwoFactorDisabledEvent(userId, wasAdminOverride: false);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Audit log should still be created
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        // Email should not be sent for non-existent user
        _mockEmailService.Verify(
            e => e.SendTwoFactorDisabledEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailServiceFails_StillCreatesAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new TwoFactorDisabledEvent(userId, wasAdminOverride: false);

        var testUser = new UserBuilder()
            .WithId(userId)
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testUser);

        _mockEmailService
            .Setup(e => e.SendTwoFactorDisabledEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Email service unavailable"));

        // Act - Should not throw
        await _handler.Handle(@event, CancellationToken.None);

        // Assert - Audit log should still be created
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        // Error should be logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send 2FA disabled email")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CapturesWasAdminOverrideInMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new TwoFactorDisabledEvent(userId, wasAdminOverride: false);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("WasAdminOverride");
        auditLog.Details.Should().Contain("Action");
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