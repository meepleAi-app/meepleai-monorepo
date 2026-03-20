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
/// Unit tests for <see cref="OAuthAccountLinkedEventHandler"/>.
/// Tests audit logging for OAuth account linking events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class OAuthAccountLinkedEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<OAuthAccountLinkedEventHandler>> _mockLogger;
    private readonly OAuthAccountLinkedEventHandler _handler;
    private bool _disposed;

    public OAuthAccountLinkedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<OAuthAccountLinkedEventHandler>>();
        _handler = new OAuthAccountLinkedEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithGoogleProvider_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var provider = "google";
        var providerUserId = "google-user-12345";
        var @event = new OAuthAccountLinkedEvent(userId, provider, providerUserId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.UserId.Should().Be(userId);
        auditLog.Resource.Should().Be(nameof(OAuthAccountLinkedEvent));
        auditLog.Action.Should().Contain("OAuthAccountLinkedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("google");
        auditLog.Details.Should().Contain("google-user-12345");
    }

    [Fact]
    public async Task Handle_WithDiscordProvider_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var provider = "discord";
        var providerUserId = "discord-123456789";
        var @event = new OAuthAccountLinkedEvent(userId, provider, providerUserId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("discord");
        auditLog.Details.Should().Contain("discord-123456789");
        auditLog.Details.Should().Contain("OAuthAccountLinked");
    }

    [Fact]
    public async Task Handle_WithGitHubProvider_CreatesAuditLogEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var provider = "github";
        var providerUserId = "github-user-98765";
        var @event = new OAuthAccountLinkedEvent(userId, provider, providerUserId);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("github");
        auditLog.Details.Should().Contain("github-user-98765");
    }

    [Fact]
    public async Task Handle_LogsEventHandlingWithProviderInfo()
    {
        // Arrange
        var @event = new OAuthAccountLinkedEvent(
            Guid.NewGuid(),
            "google",
            "provider-id-123");

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
    public async Task Handle_CapturesAllMetadataFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new OAuthAccountLinkedEvent(userId, "google", "user-123");

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("UserId");
        auditLog.Details.Should().Contain("Provider");
        auditLog.Details.Should().Contain("ProviderUserId");
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