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
/// Unit tests for <see cref="SessionRevokedEventHandler"/>.
/// Tests audit logging for session revocation events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SessionRevokedEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<SessionRevokedEventHandler>> _mockLogger;
    private readonly SessionRevokedEventHandler _handler;
    private bool _disposed;

    public SessionRevokedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<SessionRevokedEventHandler>>();
        _handler = new SessionRevokedEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithReason_CreatesAuditLogEntry()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "User logged out";
        var @event = new SessionRevokedEvent(sessionId, userId, reason);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(SessionRevokedEvent));
        auditLog.Action.Should().Contain("SessionRevokedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain(sessionId.ToString());
        auditLog.Details.Should().Contain(reason);
    }

    [Fact]
    public async Task Handle_WithoutReason_CreatesAuditLogWithNullReason()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new SessionRevokedEvent(sessionId, userId, reason: null);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.UserId.Should().Be(userId);
        auditLog.Details.Should().Contain("SessionRevoked");
    }

    [Fact]
    public async Task Handle_WithAdminRevocation_CreatesAuditLog()
    {
        // Arrange - Admin revoked user's session
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "Admin terminated session due to security concern";
        var @event = new SessionRevokedEvent(sessionId, userId, reason);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("Admin terminated");
        auditLog.Details.Should().Contain("security concern");
    }

    [Fact]
    public async Task Handle_CapturesSessionIdAndUserId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var @event = new SessionRevokedEvent(sessionId, userId, "test");

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain(sessionId.ToString());
        auditLog.Details.Should().Contain(userId.ToString());
        auditLog.Details.Should().Contain("SessionId");
        auditLog.Details.Should().Contain("Action");
    }

    [Fact]
    public async Task Handle_LogsSuccessfulEventHandling()
    {
        // Arrange
        var @event = new SessionRevokedEvent(Guid.NewGuid(), Guid.NewGuid(), "test");

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