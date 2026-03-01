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
/// Unit tests for <see cref="PasswordChangedEventHandler"/>.
/// Tests audit logging for password change events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PasswordChangedEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<PasswordChangedEventHandler>> _mockLogger;
    private readonly PasswordChangedEventHandler _handler;
    private bool _disposed;

    public PasswordChangedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<PasswordChangedEventHandler>>();
        _handler = new PasswordChangedEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidEvent_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new PasswordChangedEvent(userId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(PasswordChangedEvent));
        auditLog.Action.Should().Contain("PasswordChangedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("PasswordChanged");
    }

    [Fact]
    public async Task Handle_CapturesUserIdInMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new PasswordChangedEvent(userId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain(userId.ToString());
        auditLog.Details.Should().Contain("Action");
    }

    [Fact]
    public async Task Handle_LogsSuccessfulEventHandling()
    {
        // Arrange
        var @event = new PasswordChangedEvent(Guid.NewGuid());

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
    public async Task Handle_MultipleEvents_CreatesMultipleAuditLogs()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var event1 = new PasswordChangedEvent(userId1);
        var event2 = new PasswordChangedEvent(userId2);

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
