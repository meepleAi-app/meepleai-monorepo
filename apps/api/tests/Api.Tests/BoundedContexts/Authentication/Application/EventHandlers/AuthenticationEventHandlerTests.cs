using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for Authentication domain event handlers.
/// Issue #1534: Audit persistence is now centralised in <c>DomainEventAuditHandler</c> and is covered
/// by <c>DomainEventAuditHandlerTests</c>. Per-handler logger smoke tests are retained here.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AuthenticationEventHandlerTests
{
    [Fact]
    public async Task EmailChangedEventHandler_Handle_LogsHandlingInformation()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<EmailChangedEventHandler>>();
        var handler = new EmailChangedEventHandler(dbContext, logger.Object);

        var @event = new EmailChangedEvent(
            Guid.NewGuid(),
            Email.Parse("old@example.com"),
            Email.Parse("new@example.com"));

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully handled")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OAuthAccountLinkedEventHandler_Handle_LogsHandlingInformation()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthAccountLinkedEventHandler>>();
        var handler = new OAuthAccountLinkedEventHandler(dbContext, logger.Object);

        var @event = new OAuthAccountLinkedEvent(Guid.NewGuid(), "google", "google-user-123");

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully handled")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OAuthAccountUnlinkedEventHandler_Handle_LogsHandlingInformation()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthAccountUnlinkedEventHandler>>();
        var handler = new OAuthAccountUnlinkedEventHandler(dbContext, logger.Object);

        var @event = new OAuthAccountUnlinkedEvent(Guid.NewGuid(), "google");

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully handled")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OAuthTokensRefreshedEventHandler_Handle_LogsHandlingInformation()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var logger = new Mock<ILogger<OAuthTokensRefreshedEventHandler>>();
        var handler = new OAuthTokensRefreshedEventHandler(dbContext, logger.Object);

        var @event = new OAuthTokensRefreshedEvent(Guid.NewGuid(), "google", DateTime.UtcNow.AddHours(1));

        // Act
        await handler.Handle(@event, CancellationToken.None);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully handled")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
