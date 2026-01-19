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
/// Unit tests for <see cref="OAuthTokensRefreshedEventHandler"/>.
/// Tests audit logging for OAuth token refresh events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class OAuthTokensRefreshedEventHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<OAuthTokensRefreshedEventHandler>> _mockLogger;
    private readonly OAuthTokensRefreshedEventHandler _handler;
    private bool _disposed;

    public OAuthTokensRefreshedEventHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLogger = new Mock<ILogger<OAuthTokensRefreshedEventHandler>>();
        _handler = new OAuthTokensRefreshedEventHandler(_dbContext, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithGoogleProvider_CreatesAuditLogEntry()
    {
        // Arrange
        var oauthAccountId = Guid.NewGuid();
        var provider = "google";
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var @event = new OAuthTokensRefreshedEvent(oauthAccountId, provider, expiresAt);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        // Note: UserId is null for OAuthTokensRefreshed as it's account-level, not user-level
        auditLog.UserId.Should().BeNull();
        auditLog.Resource.Should().Be(nameof(OAuthTokensRefreshedEvent));
        auditLog.Action.Should().Contain("OAuthTokensRefreshedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("google");
    }

    [Fact]
    public async Task Handle_CapturesOAuthAccountIdAndProvider()
    {
        // Arrange
        var oauthAccountId = Guid.NewGuid();
        var provider = "discord";
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var @event = new OAuthTokensRefreshedEvent(oauthAccountId, provider, expiresAt);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain(oauthAccountId.ToString());
        auditLog.Details.Should().Contain("discord");
        auditLog.Details.Should().Contain("OAuthTokensRefreshed");
    }

    [Fact]
    public async Task Handle_CapturesExpiresAtTimestamp()
    {
        // Arrange
        var oauthAccountId = Guid.NewGuid();
        var expiresAt = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var @event = new OAuthTokensRefreshedEvent(oauthAccountId, "github", expiresAt);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = await _dbContext.AuditLogs.FirstAsync();
        auditLog.Details.Should().Contain("ExpiresAt");
        auditLog.Details.Should().Contain("2025");
    }

    [Fact]
    public async Task Handle_LogsSuccessfulEventHandling()
    {
        // Arrange
        var @event = new OAuthTokensRefreshedEvent(
            Guid.NewGuid(),
            "google",
            DateTime.UtcNow.AddHours(1));

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
    public async Task Handle_WithNearExpirationTime_CreatesAuditLog()
    {
        // Arrange - Token expiring soon
        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        var @event = new OAuthTokensRefreshedEvent(Guid.NewGuid(), "google", expiresAt);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLogs = await _dbContext.AuditLogs.ToListAsync();
        auditLogs.Should().HaveCount(1);
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