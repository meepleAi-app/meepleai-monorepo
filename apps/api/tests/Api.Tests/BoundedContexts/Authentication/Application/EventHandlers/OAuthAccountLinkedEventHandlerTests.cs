using Api.BoundedContexts.Authentication.Application.EventHandlers;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="OAuthAccountLinkedEventHandler"/>.
/// Issue #1534: Audit persistence is now centralised in <c>DomainEventAuditHandler</c> and is covered
/// by <c>DomainEventAuditHandlerTests</c>. Handler-specific tests only verify logging hooks here.
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
    public async Task Handle_LogsHandlingInformation()
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
