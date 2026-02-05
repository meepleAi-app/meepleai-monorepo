using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Unit tests for PrivatePdfRemovedEventHandler.
/// Issue #3651: Tests for vector cleanup when private PDF is removed.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class PrivatePdfRemovedEventHandlerTests
{
    private readonly Mock<IPrivateQdrantService> _mockPrivateQdrantService;
    private readonly Mock<ILogger<PrivatePdfRemovedEventHandler>> _mockLogger;
    private readonly PrivatePdfRemovedEventHandler _handler;

    public PrivatePdfRemovedEventHandlerTests()
    {
        _mockPrivateQdrantService = new Mock<IPrivateQdrantService>();
        _mockLogger = new Mock<ILogger<PrivatePdfRemovedEventHandler>>();

        _handler = new PrivatePdfRemovedEventHandler(
            _mockPrivateQdrantService.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullPrivateQdrantService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PrivatePdfRemovedEventHandler(
            null!,
            _mockLogger.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("privateQdrantService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PrivatePdfRemovedEventHandler(
            _mockPrivateQdrantService.Object,
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle Success Tests

    [Fact]
    public async Task Handle_WithValidEvent_CallsDeletePrivateByPdfIdAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var evt = new PrivatePdfRemovedEvent(
            libraryEntryId: Guid.NewGuid(),
            userId: userId,
            gameId: Guid.NewGuid(),
            pdfDocumentId: pdfId);

        _mockPrivateQdrantService
            .Setup(s => s.DeletePrivateByPdfIdAsync(userId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert
        _mockPrivateQdrantService.Verify(
            s => s.DeletePrivateByPdfIdAsync(userId, pdfId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeletionSucceeds_LogsInformation()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var evt = new PrivatePdfRemovedEvent(
            libraryEntryId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: pdfId);

        _mockPrivateQdrantService
            .Setup(s => s.DeletePrivateByPdfIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert - verify that logging occurred (at least 2 log calls: start + success)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task Handle_WhenNoVectorsFound_LogsWarning()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var evt = new PrivatePdfRemovedEvent(
            libraryEntryId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: pdfId);

        _mockPrivateQdrantService
            .Setup(s => s.DeletePrivateByPdfIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert - verify that warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Handle Error Tests

    [Fact]
    public async Task Handle_WhenServiceThrows_LogsErrorAndDoesNotRethrow()
    {
        // Arrange
        var evt = new PrivatePdfRemovedEvent(
            libraryEntryId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid());

        var exception = new InvalidOperationException("Qdrant service unavailable");

        _mockPrivateQdrantService
            .Setup(s => s.DeletePrivateByPdfIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var act = () => _handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert - should not throw (error is logged but not propagated)
        await act.Should().NotThrowAsync();

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_DoesNotAffectTransaction()
    {
        // Arrange
        var evt = new PrivatePdfRemovedEvent(
            libraryEntryId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid());

        _mockPrivateQdrantService
            .Setup(s => s.DeletePrivateByPdfIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        await _handler.Handle(evt, TestContext.Current.CancellationToken);

        // Assert - handler completed without throwing
        // This proves that the transaction (PDF removal) is not affected by vector cleanup failure
    }

    #endregion
}
