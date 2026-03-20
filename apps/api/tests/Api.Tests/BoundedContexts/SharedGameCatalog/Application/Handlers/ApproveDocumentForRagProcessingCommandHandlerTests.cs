using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for ApproveDocumentForRagProcessingCommandHandler.
/// Tests document approval workflow for RAG processing.
/// Issue #3533: Admin API Endpoints - Document Approval
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ApproveDocumentForRagProcessingCommandHandlerTests
{
    private readonly Mock<ISharedGameDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<ApproveDocumentForRagProcessingCommandHandler>> _loggerMock;
    private readonly ApproveDocumentForRagProcessingCommandHandler _handler;

    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly Guid TestPdfDocumentId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestAdminId = Guid.NewGuid();

    public ApproveDocumentForRagProcessingCommandHandlerTests()
    {
        _documentRepositoryMock = new Mock<ISharedGameDocumentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<ApproveDocumentForRagProcessingCommandHandler>>();

        _handler = new ApproveDocumentForRagProcessingCommandHandler(
            _documentRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    private static SharedGameDocument CreatePendingDocument()
    {
        return SharedGameDocument.Create(
            sharedGameId: TestGameId,
            pdfDocumentId: TestPdfDocumentId,
            documentType: SharedGameDocumentType.Rulebook,
            version: "1.0",
            createdBy: TestUserId);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithPendingDocument_ApprovesSuccessfully()
    {
        // Arrange
        var document = CreatePendingDocument();
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: document.Id,
            ApprovedBy: TestAdminId,
            Notes: "Approved for processing");

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        document.ApprovalStatus.Should().Be(DocumentApprovalStatus.Approved);
        document.ApprovedBy.Should().Be(TestAdminId);
        document.ApprovedAt.Should().NotBeNull();
        document.ApprovalNotes.Should().Be("Approved for processing");
    }

    [Fact]
    public async Task Handle_WithPendingDocument_SavesChanges()
    {
        // Arrange
        var document = CreatePendingDocument();
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: document.Id,
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPendingDocument_PublishesDomainEvent()
    {
        // Arrange
        var document = CreatePendingDocument();
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: document.Id,
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        DocumentApprovedForRagEvent? capturedEvent = null;
        _mediatorMock
            .Setup(m => m.Publish(It.IsAny<DocumentApprovedForRagEvent>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((evt, _) => capturedEvent = evt as DocumentApprovedForRagEvent);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<DocumentApprovedForRagEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedEvent.Should().NotBeNull();
        capturedEvent.DocumentId.Should().Be(document.Id);
        capturedEvent.SharedGameId.Should().Be(TestGameId);
        capturedEvent.PdfDocumentId.Should().Be(TestPdfDocumentId);
        capturedEvent.ApprovedBy.Should().Be(TestAdminId);
    }

    [Fact]
    public async Task Handle_WithoutNotes_ApprovesWithNullNotes()
    {
        // Arrange
        var document = CreatePendingDocument();
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: document.Id,
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        document.ApprovalStatus.Should().Be(DocumentApprovalStatus.Approved);
        document.ApprovalNotes.Should().BeNull();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Handle_WithNonExistentDocument_ThrowsNotFoundException()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(command.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameDocument?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.ResourceType.Should().Be("SharedGameDocument");
    }

    [Fact]
    public async Task Handle_WithAlreadyApprovedDocument_ThrowsConflictException()
    {
        // Arrange
        var document = CreatePendingDocument();
        document.Approve(Guid.NewGuid()); // Already approved

        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: document.Id,
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<ConflictException>()).Which;

        exception.Message.Should().Contain("Document approval failed");
    }

    [Fact]
    public async Task Handle_WithRejectedDocument_ThrowsConflictException()
    {
        // Arrange
        var document = CreatePendingDocument();
        document.Reject(Guid.NewGuid(), "Quality issues"); // Already rejected

        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: document.Id,
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<ConflictException>()).Which;

        exception.Message.Should().Contain("Document approval failed");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Repository Interaction Tests

    [Fact]
    public async Task Handle_CallsGetByIdWithCorrectParameters()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = CreatePendingDocument();

        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: documentId,
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _documentRepositoryMock.Verify(
            r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotSaveChangesOnError()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(command.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameDocument?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotPublishEventOnError()
    {
        // Arrange
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: Guid.NewGuid(),
            ApprovedBy: TestAdminId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(command.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameDocument?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();

        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<DocumentApprovedForRagEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task Handle_WithCancellationRequested_CancelsOperation()
    {
        // Arrange
        var document = CreatePendingDocument();
        var command = new ApproveDocumentForRagProcessingCommand(
            DocumentId: document.Id,
            ApprovedBy: TestAdminId);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = () => _handler.Handle(command, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}
