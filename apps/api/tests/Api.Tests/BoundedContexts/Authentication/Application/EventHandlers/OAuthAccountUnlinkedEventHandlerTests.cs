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
/// Unit tests for <see cref="OAuthAccountUnlinkedEventHandler"/>.
/// Tests audit logging for OAuth account unlinking events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class OAuthAccountUnlinkedEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<OAuthAccountUnlinkedEventHandler>> _mockLogger;
    private readonly OAuthAccountUnlinkedEventHandler _handler;
    private bool _disposed;

    public OAuthAccountUnlinkedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<OAuthAccountUnlinkedEventHandler>>();
        _handler = new OAuthAccountUnlinkedEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithGoogleProvider_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var provider = "google";
        var @event = new OAuthAccountUnlinkedEvent(userId, provider);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(OAuthAccountUnlinkedEvent));
        auditLog.Action.Should().Contain("OAuthAccountUnlinkedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("google");
    }

    [Fact]
    public async Task Handle_WithDiscordProvider_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var provider = "discord";
        var @event = new OAuthAccountUnlinkedEvent(userId, provider);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("discord");
        auditLog.Details.Should().Contain("OAuthAccountUnlinked");
    }

    [Fact]
    public async Task Handle_WithGitHubProvider_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var provider = "github";
        var @event = new OAuthAccountUnlinkedEvent(userId, provider);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("github");
    }

    [Fact]
    public async Task Handle_CapturesUserIdAndProvider()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new OAuthAccountUnlinkedEvent(userId, "google");

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.UserId.Should().Be(userId);
        auditLog.Details.Should().Contain(userId.ToString());
        auditLog.Details.Should().Contain("google");
        auditLog.Details.Should().Contain("Action");
    }

    [Fact]
    public async Task Handle_LogsSuccessfulEventHandling()
    {
        // Arrange
        var @event = new OAuthAccountUnlinkedEvent(Guid.NewGuid(), "google");

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
