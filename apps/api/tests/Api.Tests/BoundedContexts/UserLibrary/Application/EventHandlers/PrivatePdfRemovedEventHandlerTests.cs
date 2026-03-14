using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Unit tests for PrivatePdfRemovedEventHandler.
/// Issue #3651: Tests for vector cleanup when private PDF is removed.
/// NOTE: Vector store (Qdrant) has been removed — handler is now a no-op that only logs.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class PrivatePdfRemovedEventHandlerTests
{
    private readonly Mock<ILogger<PrivatePdfRemovedEventHandler>> _mockLogger;
    private readonly PrivatePdfRemovedEventHandler _handler;

    public PrivatePdfRemovedEventHandlerTests()
    {
        _mockLogger = new Mock<ILogger<PrivatePdfRemovedEventHandler>>();

        _handler = new PrivatePdfRemovedEventHandler(
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PrivatePdfRemovedEventHandler(
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle Tests

    [Fact]
    public async Task Handle_WithValidEvent_CompletesSuccessfully()
    {
        // Arrange
        var evt = new PrivatePdfRemovedEvent(
            libraryEntryId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert - handler is a no-op, should complete without throwing
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WithValidEvent_LogsInformation()
    {
        // Arrange
        var evt = new PrivatePdfRemovedEvent(
            libraryEntryId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid());

        // Act
        await _handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert - verify that logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
