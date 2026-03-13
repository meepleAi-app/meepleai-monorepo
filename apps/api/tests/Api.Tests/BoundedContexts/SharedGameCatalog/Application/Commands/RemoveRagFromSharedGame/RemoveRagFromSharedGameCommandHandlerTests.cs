using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;

[Trait("Category", TestCategories.Unit)]
public class RemoveRagFromSharedGameCommandHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ISharedGameDocumentRepository> _documentRepositoryMock;
    private readonly Mock<ILogger<RemoveRagFromSharedGameCommandHandler>> _loggerMock;
    private readonly RemoveRagFromSharedGameCommandHandler _handler;

    private static readonly Guid SharedGameId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid PdfDocumentId = Guid.NewGuid();

    public RemoveRagFromSharedGameCommandHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _documentRepositoryMock = new Mock<ISharedGameDocumentRepository>();
        _loggerMock = new Mock<ILogger<RemoveRagFromSharedGameCommandHandler>>();
        _handler = new RemoveRagFromSharedGameCommandHandler(
            _mediatorMock.Object,
            _documentRepositoryMock.Object,
            _loggerMock.Object);
    }

    private static SharedGameDocument CreateDocument(
        Guid? id = null,
        Guid? sharedGameId = null,
        Guid? pdfDocumentId = null,
        bool isActive = true,
        SharedGameDocumentType documentType = SharedGameDocumentType.Rulebook,
        string version = "1.0")
    {
        return new SharedGameDocument(
            id: id ?? Guid.NewGuid(),
            sharedGameId: sharedGameId ?? SharedGameId,
            pdfDocumentId: pdfDocumentId ?? PdfDocumentId,
            documentType: documentType,
            version: version,
            isActive: isActive,
            tags: null,
            createdAt: DateTime.UtcNow,
            createdBy: UserId);
    }

    private void SetupDeletePdfSuccess()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeletePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDeleteResult(Success: true, Message: "Deleted", GameId: null));
    }

    // ========================================
    // Happy Path
    // ========================================

    [Fact]
    public async Task Handle_HappyPath_RemovesDocumentAndDeletesPdf()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var document = CreateDocument(id: docId, isActive: false);
        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        SetupDeletePdfSuccess();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(docId, result.RemovedSharedGameDocumentId);
        Assert.Equal(PdfDocumentId, result.RemovedPdfDocumentId);
        Assert.True(result.QdrantCleanupSucceeded);
        Assert.True(result.BlobCleanupSucceeded);

        _mediatorMock.Verify(m => m.Send(
            It.Is<RemoveDocumentFromSharedGameCommand>(c => c.SharedGameId == SharedGameId && c.DocumentId == docId),
            It.IsAny<CancellationToken>()), Times.Once);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeletePdfCommand>(c => c.PdfId == PdfDocumentId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ========================================
    // Not Found
    // ========================================

    [Fact]
    public async Task Handle_DocumentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameDocument?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DocumentBelongsToDifferentGame_ThrowsNotFoundException()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        var document = CreateDocument(id: docId, sharedGameId: otherGameId);
        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    // ========================================
    // Active Version Promotion
    // ========================================

    [Fact]
    public async Task Handle_ActiveDocumentWithAlternatives_PromotesNextVersion()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var nextDocId = Guid.NewGuid();
        var activeDoc = CreateDocument(id: docId, isActive: true, version: "1.0");
        var nextDoc = CreateDocument(id: nextDocId, isActive: false, version: "2.0");
        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeDoc);

        _documentRepositoryMock
            .Setup(r => r.GetBySharedGameIdAndTypeAsync(SharedGameId, SharedGameDocumentType.Rulebook, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument> { activeDoc, nextDoc });

        SetupDeletePdfSuccess();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _documentRepositoryMock.Verify(r => r.Update(
            It.Is<SharedGameDocument>(d => d.Id == nextDocId)), Times.Once);
    }

    [Fact]
    public async Task Handle_ActiveDocumentNoAlternatives_RemovesWithoutPromotion()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var activeDoc = CreateDocument(id: docId, isActive: true);
        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeDoc);

        _documentRepositoryMock
            .Setup(r => r.GetBySharedGameIdAndTypeAsync(SharedGameId, SharedGameDocumentType.Rulebook, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SharedGameDocument> { activeDoc });

        SetupDeletePdfSuccess();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(docId, result.RemovedSharedGameDocumentId);
        _documentRepositoryMock.Verify(r => r.Update(It.IsAny<SharedGameDocument>()), Times.Never);
    }

    // ========================================
    // Graceful Degradation
    // ========================================

    [Fact]
    public async Task Handle_DeletePdfFails_ReturnsCleanupFalseFlags()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var document = CreateDocument(id: docId, isActive: false);
        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeletePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfDeleteResult(Success: false, Message: "Qdrant unavailable", GameId: null));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.QdrantCleanupSucceeded);
        Assert.False(result.BlobCleanupSucceeded);
        // SharedGameDocument was still unlinked
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<RemoveDocumentFromSharedGameCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DeletePdfThrowsException_ReturnsCleanupFalseFlags()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var document = CreateDocument(id: docId, isActive: false);
        var command = new RemoveRagFromSharedGameCommand(SharedGameId, docId, UserId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeletePdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.QdrantCleanupSucceeded);
        Assert.False(result.BlobCleanupSucceeded);
        Assert.Equal(docId, result.RemovedSharedGameDocumentId);
    }
}
