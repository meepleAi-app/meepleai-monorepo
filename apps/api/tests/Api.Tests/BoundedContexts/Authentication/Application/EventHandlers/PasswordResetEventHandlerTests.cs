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
/// Unit tests for <see cref="PasswordResetEventHandler"/>.
/// Tests audit logging for password reset events (admin-initiated).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PasswordResetEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<PasswordResetEventHandler>> _mockLogger;
    private readonly PasswordResetEventHandler _handler;
    private bool _disposed;

    public PasswordResetEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<PasswordResetEventHandler>>();
        _handler = new PasswordResetEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithAdminReset_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var @event = new PasswordResetEvent(userId, adminUserId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(PasswordResetEvent));
        auditLog.Action.Should().Contain("PasswordResetEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("PasswordReset");
        auditLog.Details.Should().Contain(adminUserId.ToString());
    }

    [Fact]
    public async Task Handle_WithSelfReset_CreatesAuditLogWithNullResetBy()
    {
        // Arrange - User initiated password reset (no admin)
        var userId = Guid.NewGuid();
        var @event = new PasswordResetEvent(userId, resetByUserId: null);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.UserId.Should().Be(userId);
        auditLog.Details.Should().Contain("PasswordReset");
        auditLog.Details.Should().Contain("ResetByUserId");
    }

    [Fact]
    public async Task Handle_CapturesUserIdAndResetByUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var @event = new PasswordResetEvent(userId, adminUserId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain(userId.ToString());
        auditLog.Details.Should().Contain(adminUserId.ToString());
        auditLog.Details.Should().Contain("Action");
    }

    [Fact]
    public async Task Handle_LogsSuccessfulEventHandling()
    {
        // Arrange
        var @event = new PasswordResetEvent(Guid.NewGuid(), Guid.NewGuid());

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
