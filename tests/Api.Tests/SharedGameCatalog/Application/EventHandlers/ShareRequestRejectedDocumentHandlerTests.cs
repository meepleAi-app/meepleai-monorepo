using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Application.EventHandlers;

public sealed class ShareRequestRejectedDocumentHandlerTests
{
    private readonly Mock<IShareRequestDocumentService> _documentService;
    private readonly Mock<MeepleAiDbContext> _dbContext;
    private readonly Mock<ILogger<ShareRequestRejectedDocumentHandler>> _logger;
    private readonly ShareRequestRejectedDocumentHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public ShareRequestRejectedDocumentHandlerTests()
    {
        _documentService = new Mock<IShareRequestDocumentService>();
        _dbContext = new Mock<MeepleAiDbContext>();
        _logger = new Mock<ILogger<ShareRequestRejectedDocumentHandler>>();

        _sut = new ShareRequestRejectedDocumentHandler(
            _dbContext.Object,
            _documentService.Object,
            _logger.Object);
    }

    [Fact]
    public async Task HandleEventAsync_CallsCleanupOrphanedDocuments()
    {
        // Arrange
        var domainEvent = new ShareRequestRejectedEvent(
            _shareRequestId,
            _adminId,
            "Not suitable");

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _documentService.Verify(s => s.CleanupOrphanedDocuments(
            _shareRequestId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_WhenCleanupFails_DoesNotThrow()
    {
        // Arrange
        var domainEvent = new ShareRequestRejectedEvent(
            _shareRequestId,
            _adminId,
            "Not suitable");

        _documentService.Setup(s => s.CleanupOrphanedDocuments(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cleanup error"));

        // Act
        var act = async () => await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
