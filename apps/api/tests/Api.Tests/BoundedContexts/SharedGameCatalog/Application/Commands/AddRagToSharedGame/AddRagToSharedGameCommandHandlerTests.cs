using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

[Trait("Category", TestCategories.Unit)]
public class AddRagToSharedGameCommandHandlerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ISharedGameRepository> _sharedGameRepositoryMock;
    private readonly Mock<ILogger<AddRagToSharedGameCommandHandler>> _loggerMock;
    private readonly AddRagToSharedGameCommandHandler _handler;

    public AddRagToSharedGameCommandHandlerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _sharedGameRepositoryMock = new Mock<ISharedGameRepository>();
        _loggerMock = new Mock<ILogger<AddRagToSharedGameCommandHandler>>();
        _handler = new AddRagToSharedGameCommandHandler(
            _mediatorMock.Object,
            _sharedGameRepositoryMock.Object,
            _loggerMock.Object);
    }

    private static AddRagToSharedGameCommand CreateCommand(
        Guid? sharedGameId = null,
        Guid? userId = null,
        bool isAdmin = true)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.FileName).Returns("rulebook.pdf");

        return new AddRagToSharedGameCommand(
            SharedGameId: sharedGameId ?? Guid.NewGuid(),
            File: fileMock.Object,
            DocumentType: SharedGameDocumentType.Rulebook,
            Version: "1.0",
            Tags: null,
            UserId: userId ?? Guid.NewGuid(),
            IsAdmin: isAdmin);
    }

    private void SetupSharedGameExists(Guid sharedGameId)
    {
        var sharedGame = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2024,
            description: "A test game",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByIdAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);
    }

    private void SetupPdfUploadSuccess(Guid pdfDocumentId)
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UploadPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfUploadResult(
                Success: true,
                Message: "Upload successful",
                Document: new PdfDocumentDto(
                    Id: pdfDocumentId,
                    GameId: null,
                    FileName: "rulebook.pdf",
                    FilePath: "/uploads/rulebook.pdf",
                    FileSizeBytes: 1024,
                    ProcessingStatus: "Uploaded",
                    UploadedAt: DateTime.UtcNow,
                    ProcessedAt: null,
                    PageCount: null)));
    }

    private void SetupAddDocumentSuccess(Guid sharedGameDocumentId)
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddDocumentToSharedGameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGameDocumentId);
    }

    private void SetupApproveSuccess()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ApproveDocumentForRagProcessingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);
    }

    private void SetupEnqueueSuccess(Guid processingJobId)
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingJobId);
    }

    [Fact]
    public async Task Handle_SharedGameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var command = CreateCommand(sharedGameId: sharedGameId);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByIdAsync(sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Equal("SharedGame", ex.ResourceType);
        Assert.Equal(sharedGameId.ToString(), ex.ResourceId);
    }

    [Fact]
    public async Task Handle_AdminUser_ExecutesAllFourSteps()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var sharedGameDocumentId = Guid.NewGuid();
        var processingJobId = Guid.NewGuid();

        var command = CreateCommand(sharedGameId: sharedGameId, isAdmin: true);

        SetupSharedGameExists(sharedGameId);
        SetupPdfUploadSuccess(pdfDocumentId);
        SetupAddDocumentSuccess(sharedGameDocumentId);
        SetupApproveSuccess();
        SetupEnqueueSuccess(processingJobId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(pdfDocumentId, result.PdfDocumentId);
        Assert.Equal(sharedGameDocumentId, result.SharedGameDocumentId);
        Assert.Equal(processingJobId, result.ProcessingJobId);
        Assert.True(result.AutoApproved);
        Assert.Equal($"/api/v1/pdf/{pdfDocumentId}/status/stream", result.StreamUrl);

        // Verify all 4 sub-commands were called
        _mediatorMock.Verify(m => m.Send(It.IsAny<UploadPdfCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(It.IsAny<AddDocumentToSharedGameCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(It.IsAny<ApproveDocumentForRagProcessingCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EditorUser_OnlyUploadsAndLinks()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var sharedGameDocumentId = Guid.NewGuid();

        var command = CreateCommand(sharedGameId: sharedGameId, isAdmin: false);

        SetupSharedGameExists(sharedGameId);
        SetupPdfUploadSuccess(pdfDocumentId);
        SetupAddDocumentSuccess(sharedGameDocumentId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(pdfDocumentId, result.PdfDocumentId);
        Assert.Equal(sharedGameDocumentId, result.SharedGameDocumentId);
        Assert.Null(result.ProcessingJobId);
        Assert.False(result.AutoApproved);

        // Verify only upload + link called (NOT approve or enqueue)
        _mediatorMock.Verify(m => m.Send(It.IsAny<UploadPdfCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(It.IsAny<AddDocumentToSharedGameCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(It.IsAny<ApproveDocumentForRagProcessingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediatorMock.Verify(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EnqueueFails_GracefulDegradation()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var sharedGameDocumentId = Guid.NewGuid();

        var command = CreateCommand(sharedGameId: sharedGameId, isAdmin: true);

        SetupSharedGameExists(sharedGameId);
        SetupPdfUploadSuccess(pdfDocumentId);
        SetupAddDocumentSuccess(sharedGameDocumentId);
        SetupApproveSuccess();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Queue is full"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — approval happened but enqueue failed gracefully
        Assert.Equal(pdfDocumentId, result.PdfDocumentId);
        Assert.Equal(sharedGameDocumentId, result.SharedGameDocumentId);
        Assert.Null(result.ProcessingJobId);
        Assert.True(result.AutoApproved);
    }

    [Fact]
    public async Task Handle_PdfUploadFails_ThrowsConflictException()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var command = CreateCommand(sharedGameId: sharedGameId, isAdmin: true);

        SetupSharedGameExists(sharedGameId);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UploadPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PdfUploadResult(Success: false, Message: "Invalid file", Document: null));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("PDF upload failed", ex.Message);
    }

    [Fact]
    public async Task Handle_AdminUser_EnqueueUsesHighPriority()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var sharedGameDocumentId = Guid.NewGuid();

        var command = CreateCommand(sharedGameId: sharedGameId, isAdmin: true);

        SetupSharedGameExists(sharedGameId);
        SetupPdfUploadSuccess(pdfDocumentId);
        SetupAddDocumentSuccess(sharedGameDocumentId);
        SetupApproveSuccess();
        SetupEnqueueSuccess(Guid.NewGuid());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert — verify high priority was used
        _mediatorMock.Verify(m => m.Send(
            It.Is<EnqueuePdfCommand>(c => c.Priority == (int)ProcessingPriority.High),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
