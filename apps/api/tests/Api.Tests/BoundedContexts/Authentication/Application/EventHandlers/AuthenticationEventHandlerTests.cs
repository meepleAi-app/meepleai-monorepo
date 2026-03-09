using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for Authentication domain event handlers.
/// Issue #2645: Event handler tests for ApiKeyRevoked, EmailChanged, OAuth events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AuthenticationEventHandlerTests
{
    #region ApiKeyRevokedEventHandler Tests

    [Fact]
    public async Task ApiKeyRevokedEventHandler_Handle_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<ApiKeyRevokedEventHandler>>();
        var handler = new ApiKeyRevokedEventHandler(dbContext, logger.Object);

        var apiKeyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "Security compromise suspected";
        var @event = new ApiKeyRevokedEvent(apiKeyId, userId, reason);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a => a.Action.Contains("ApiKeyRevokedEvent"));
        Assert.NotNull(auditLog);
        Assert.Equal(userId, auditLog.UserId);
        Assert.Contains("ApiKeyRevoked", auditLog.Details);
        Assert.Contains(apiKeyId.ToString(), auditLog.Details);
    }

    [Fact]
    public async Task ApiKeyRevokedEventHandler_Handle_WithNullReason_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<ApiKeyRevokedEventHandler>>();
        var handler = new ApiKeyRevokedEventHandler(dbContext, logger.Object);

        var @event = new ApiKeyRevokedEvent(Guid.NewGuid(), Guid.NewGuid(), null);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.FirstOrDefault();
        Assert.NotNull(auditLog);
        Assert.Equal("Success", auditLog.Result);
    }

    [Fact]
    public async Task ApiKeyRevokedEventHandler_Handle_LogsEventInformation()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<ApiKeyRevokedEventHandler>>();
        var handler = new ApiKeyRevokedEventHandler(dbContext, logger.Object);

        var @event = new ApiKeyRevokedEvent(Guid.NewGuid(), Guid.NewGuid(), "Test reason");

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Handling domain event")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region EmailChangedEventHandler Tests

    [Fact]
    public async Task EmailChangedEventHandler_Handle_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<EmailChangedEventHandler>>();
        var handler = new EmailChangedEventHandler(dbContext, logger.Object);

        var userId = Guid.NewGuid();
        var oldEmail = Email.Parse("old@example.com");
        var newEmail = Email.Parse("new@example.com");
        var @event = new EmailChangedEvent(userId, oldEmail, newEmail);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a => a.Action.Contains("EmailChangedEvent"));
        Assert.NotNull(auditLog);
        Assert.Equal(userId, auditLog.UserId);
        Assert.Contains("EmailChanged", auditLog.Details);
        Assert.Contains("old@example.com", auditLog.Details);
        Assert.Contains("new@example.com", auditLog.Details);
    }

    [Fact]
    public async Task EmailChangedEventHandler_Handle_StoresOldAndNewEmail()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<EmailChangedEventHandler>>();
        var handler = new EmailChangedEventHandler(dbContext, logger.Object);

        var @event = new EmailChangedEvent(
            Guid.NewGuid(),
            Email.Parse("previous@test.com"),
            Email.Parse("current@test.com"));

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Contains("previous@test.com", auditLog.Details);
        Assert.Contains("current@test.com", auditLog.Details);
    }

    [Fact]
    public async Task EmailChangedEventHandler_Handle_NormalizesEmailInMetadata()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<EmailChangedEventHandler>>();
        var handler = new EmailChangedEventHandler(dbContext, logger.Object);

        // Emails with mixed case - Email value object normalizes to lowercase
        var @event = new EmailChangedEvent(
            Guid.NewGuid(),
            Email.Parse("OLD@EXAMPLE.COM"),
            Email.Parse("NEW@EXAMPLE.COM"));

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert - Emails should be normalized to lowercase
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Contains("old@example.com", auditLog.Details);
        Assert.Contains("new@example.com", auditLog.Details);
    }

    #endregion

    #region OAuthAccountLinkedEventHandler Tests

    [Fact]
    public async Task OAuthAccountLinkedEventHandler_Handle_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthAccountLinkedEventHandler>>();
        var handler = new OAuthAccountLinkedEventHandler(dbContext, logger.Object);

        var userId = Guid.NewGuid();
        var provider = "google";
        var providerUserId = "google-user-123";
        var @event = new OAuthAccountLinkedEvent(userId, provider, providerUserId);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a => a.Action.Contains("OAuthAccountLinkedEvent"));
        Assert.NotNull(auditLog);
        Assert.Equal(userId, auditLog.UserId);
        Assert.Contains("OAuthAccountLinked", auditLog.Details);
        Assert.Contains("google", auditLog.Details);
    }

    [Fact]
    public async Task OAuthAccountLinkedEventHandler_Handle_StoresProviderDetails()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthAccountLinkedEventHandler>>();
        var handler = new OAuthAccountLinkedEventHandler(dbContext, logger.Object);

        var @event = new OAuthAccountLinkedEvent(Guid.NewGuid(), "discord", "discord-id-456");

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Contains("discord", auditLog.Details);
        Assert.Contains("discord-id-456", auditLog.Details);
    }

    [Theory]
    [InlineData("google", "google-123")]
    [InlineData("github", "github-456")]
    [InlineData("discord", "discord-789")]
    public async Task OAuthAccountLinkedEventHandler_Handle_SupportsMultipleProviders(string provider, string providerUserId)
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthAccountLinkedEventHandler>>();
        var handler = new OAuthAccountLinkedEventHandler(dbContext, logger.Object);

        var @event = new OAuthAccountLinkedEvent(Guid.NewGuid(), provider, providerUserId);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Contains(provider, auditLog.Details);
        Assert.Contains(providerUserId, auditLog.Details);
    }

    #endregion

    #region OAuthAccountUnlinkedEventHandler Tests

    [Fact]
    public async Task OAuthAccountUnlinkedEventHandler_Handle_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthAccountUnlinkedEventHandler>>();
        var handler = new OAuthAccountUnlinkedEventHandler(dbContext, logger.Object);

        var userId = Guid.NewGuid();
        var provider = "google";
        var @event = new OAuthAccountUnlinkedEvent(userId, provider);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a => a.Action.Contains("OAuthAccountUnlinkedEvent"));
        Assert.NotNull(auditLog);
        Assert.Equal(userId, auditLog.UserId);
        Assert.Contains("OAuthAccountUnlinked", auditLog.Details);
    }

    [Fact]
    public async Task OAuthAccountUnlinkedEventHandler_Handle_StoresProviderName()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthAccountUnlinkedEventHandler>>();
        var handler = new OAuthAccountUnlinkedEventHandler(dbContext, logger.Object);

        var @event = new OAuthAccountUnlinkedEvent(Guid.NewGuid(), "github");

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Contains("github", auditLog.Details);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    [InlineData("discord")]
    public async Task OAuthAccountUnlinkedEventHandler_Handle_SupportsMultipleProviders(string provider)
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthAccountUnlinkedEventHandler>>();
        var handler = new OAuthAccountUnlinkedEventHandler(dbContext, logger.Object);

        var @event = new OAuthAccountUnlinkedEvent(Guid.NewGuid(), provider);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Contains(provider, auditLog.Details);
    }

    #endregion

    #region OAuthTokensRefreshedEventHandler Tests

    [Fact]
    public async Task OAuthTokensRefreshedEventHandler_Handle_CreatesAuditLog()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthTokensRefreshedEventHandler>>();
        var handler = new OAuthTokensRefreshedEventHandler(dbContext, logger.Object);

        var oauthAccountId = Guid.NewGuid();
        var provider = "google";
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var @event = new OAuthTokensRefreshedEvent(oauthAccountId, provider, expiresAt);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.FirstOrDefault(a => a.Action.Contains("OAuthTokensRefreshedEvent"));
        Assert.NotNull(auditLog);
        Assert.Null(auditLog.UserId); // Token refresh is OAuth account event, not user-specific
        Assert.Contains("OAuthTokensRefreshed", auditLog.Details);
    }

    [Fact]
    public async Task OAuthTokensRefreshedEventHandler_Handle_StoresTokenExpirationTime()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthTokensRefreshedEventHandler>>();
        var handler = new OAuthTokensRefreshedEventHandler(dbContext, logger.Object);

        var expiresAt = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var @event = new OAuthTokensRefreshedEvent(Guid.NewGuid(), "google", expiresAt);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Contains("2025", auditLog.Details); // Verify expiration year is in metadata
    }

    [Fact]
    public async Task OAuthTokensRefreshedEventHandler_Handle_HasNoUserId()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthTokensRefreshedEventHandler>>();
        var handler = new OAuthTokensRefreshedEventHandler(dbContext, logger.Object);

        var @event = new OAuthTokensRefreshedEvent(Guid.NewGuid(), "github", DateTime.UtcNow);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert - UserId should be null for OAuth token refresh (account-level, not user-level)
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Null(auditLog.UserId);
    }

    [Fact]
    public async Task OAuthTokensRefreshedEventHandler_Handle_StoresOAuthAccountId()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthTokensRefreshedEventHandler>>();
        var handler = new OAuthTokensRefreshedEventHandler(dbContext, logger.Object);

        var oauthAccountId = Guid.NewGuid();
        var @event = new OAuthTokensRefreshedEvent(oauthAccountId, "discord", DateTime.UtcNow);

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        var auditLog = dbContext.AuditLogs.Single();
        Assert.Contains(oauthAccountId.ToString(), auditLog.Details);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task EventHandler_Handle_ThrowsOnDbContextError()
    {
        // Arrange - Use disposed context to simulate error
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<ApiKeyRevokedEventHandler>>();
        var handler = new ApiKeyRevokedEventHandler(dbContext, logger.Object);

        await dbContext.DisposeAsync();

        var @event = new ApiKeyRevokedEvent(Guid.NewGuid(), Guid.NewGuid(), "Test");

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            handler.Handle(@event, CancellationToken.None));
    }

    [Fact]
    public void EventHandler_Constructor_ThrowsOnNullDbContext()
    {
        // Arrange
        var logger = new Mock<ILogger<ApiKeyRevokedEventHandler>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ApiKeyRevokedEventHandler(null!, logger.Object));
    }

    [Fact]
    public void EventHandler_Constructor_ThrowsOnNullLogger()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ApiKeyRevokedEventHandler(dbContext, null!));
    }

    #endregion
}
