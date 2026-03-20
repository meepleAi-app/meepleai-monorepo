using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="RoleChangedEventHandler"/>.
/// Tests audit logging for role change events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RoleChangedEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<RoleChangedEventHandler>> _mockLogger;
    private readonly RoleChangedEventHandler _handler;
    private bool _disposed;

    public RoleChangedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<RoleChangedEventHandler>>();
        _handler = new RoleChangedEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_UserToEditorPromotion_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new RoleChangedEvent(userId, Role.User, Role.Editor);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(RoleChangedEvent));
        auditLog.Action.Should().Contain("RoleChangedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("user");
        auditLog.Details.Should().Contain("editor");
    }

    [Fact]
    public async Task Handle_EditorToAdminPromotion_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new RoleChangedEvent(userId, Role.Editor, Role.Admin);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("editor");
        auditLog.Details.Should().Contain("admin");
        auditLog.Details.Should().Contain("RoleChanged");
    }

    [Fact]
    public async Task Handle_AdminToUserDemotion_CreatesAuditLogEntry()
    {
        // Arrange - Demotion scenario
        var userId = Guid.NewGuid();
        var @event = new RoleChangedEvent(userId, Role.Admin, Role.User);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("admin");
        auditLog.Details.Should().Contain("user");
    }

    [Fact]
    public async Task Handle_CapturesOldAndNewRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new RoleChangedEvent(userId, Role.User, Role.Admin);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("OldRole");
        auditLog.Details.Should().Contain("NewRole");
        auditLog.Details.Should().Contain("Action");
    }

    [Fact]
    public async Task Handle_LogsSuccessfulEventHandling()
    {
        // Arrange
        var @event = new RoleChangedEvent(Guid.NewGuid(), Role.User, Role.Editor);

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