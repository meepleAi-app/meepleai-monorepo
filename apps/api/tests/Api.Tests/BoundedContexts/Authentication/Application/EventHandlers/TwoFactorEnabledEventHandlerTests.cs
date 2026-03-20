using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="TwoFactorEnabledEventHandler"/>.
/// Tests audit logging for 2FA enablement events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TwoFactorEnabledEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<TwoFactorEnabledEventHandler>> _mockLogger;
    private readonly TwoFactorEnabledEventHandler _handler;
    private bool _disposed;

    public TwoFactorEnabledEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<TwoFactorEnabledEventHandler>>();
        _handler = new TwoFactorEnabledEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithStandardBackupCodes_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var backupCodesCount = 10; // Standard backup code count
        var @event = new TwoFactorEnabledEvent(userId, backupCodesCount);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(TwoFactorEnabledEvent));
        auditLog.Action.Should().Contain("TwoFactorEnabledEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("TwoFactorEnabled");
        auditLog.Details.Should().Contain("10");
    }

    [Fact]
    public async Task Handle_WithCustomBackupCodesCount_CapturesCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var backupCodesCount = 8;
        var @event = new TwoFactorEnabledEvent(userId, backupCodesCount);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("8");
        auditLog.Details.Should().Contain("BackupCodesCount");
    }

    [Fact]
    public async Task Handle_CapturesUserIdAndBackupCodesCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new TwoFactorEnabledEvent(userId, 10);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain(userId.ToString());
        auditLog.Details.Should().Contain("BackupCodesCount");
        auditLog.Details.Should().Contain("Action");
    }

    [Fact]
    public async Task Handle_LogsSuccessfulEventHandling()
    {
        // Arrange
        var @event = new TwoFactorEnabledEvent(Guid.NewGuid(), 10);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully handled")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleUsers_CreatesIndependentAuditLogs()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var event1 = new TwoFactorEnabledEvent(userId1, 10);
        var event2 = new TwoFactorEnabledEvent(userId2, 8);

        // Act
        await _handler.Handle(event1, CancellationToken.None);
        await _handler.Handle(event2, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(2);
        auditLogs.Should().Contain(log => log.UserId == userId1);
        auditLogs.Should().Contain(log => log.UserId == userId2);
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