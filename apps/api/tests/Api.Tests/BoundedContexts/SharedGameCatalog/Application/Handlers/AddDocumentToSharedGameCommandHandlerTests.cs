using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Tests for AddDocumentToSharedGameCommandHandler.
/// DocumentVersioningService methods are now virtual (fixed post-#2430).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AddDocumentToSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _gameRepositoryMock;
    private readonly Mock<ISharedGameDocumentRepository> _documentRepositoryMock;
    private readonly Mock<DocumentVersioningService> _versioningServiceMock;
    private readonly MeepleAiDbContext _context;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<AddDocumentToSharedGameCommandHandler>> _loggerMock;
    private readonly AddDocumentToSharedGameCommandHandler _handler;

    public AddDocumentToSharedGameCommandHandlerTests()
    {
        _gameRepositoryMock = new Mock<ISharedGameRepository>();
        _documentRepositoryMock = new Mock<ISharedGameDocumentRepository>();

        // Mock DocumentVersioningService requires ISharedGameDocumentRepository
        _versioningServiceMock = new Mock<DocumentVersioningService>(_documentRepositoryMock.Object);

        // Use TestDbContextFactory instead of Mock<MeepleAiDbContext> (Issue #2430)
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<AddDocumentToSharedGameCommandHandler>>();

        _handler = new AddDocumentToSharedGameCommandHandler(
            _gameRepositoryMock.Object,
            _documentRepositoryMock.Object,
            _versioningServiceMock.Object,
            _context,
            _unitOfWorkMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsDocumentSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        var createdBy = Guid.NewGuid();
        var command = new AddDocumentToSharedGameCommand(
            SharedGameId: gameId,
            PdfDocumentId: pdfId,
            DocumentType: SharedGameDocumentType.Rulebook,
            Version: "1.0",
            Tags: null,
            SetAsActive: true,
            CreatedBy: createdBy);

        // Add PDF document to context so existence check passes
        _context.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "test.pdf",
            FilePath = "/uploads/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = createdBy,
            UploadedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Mock game exists
        var game = CreateTestGame(gameId);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Mock version doesn't exist
        _versioningServiceMock
            .Setup(s => s.ValidateVersionDoesNotExistAsync(
                gameId,
                SharedGameDocumentType.Rulebook,
                "1.0",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock set active
        _versioningServiceMock
            .Setup(s => s.SetActiveVersionAsync(
                It.IsAny<SharedGameDocument>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SharedGameDocument? capturedDocument = null;
        _documentRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGameDocument>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGameDocument, CancellationToken>((d, _) => capturedDocument = d)
            .Returns(Task.CompletedTask);

        // Act
        var documentId = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, documentId);

        _documentRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<SharedGameDocument>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<SharedGameDocumentAddedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedDocument);
        Assert.Equal(gameId, capturedDocument.SharedGameId);
        Assert.Equal(pdfId, capturedDocument.PdfDocumentId);
        Assert.Equal(SharedGameDocumentType.Rulebook, capturedDocument.DocumentType);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsException()
    {
        // Arrange
        var command = new AddDocumentToSharedGameCommand(
            SharedGameId: Guid.NewGuid(),
            PdfDocumentId: Guid.NewGuid(),
            DocumentType: SharedGameDocumentType.Rulebook,
            Version: "1.0",
            Tags: null,
            SetAsActive: false,
            CreatedBy: Guid.NewGuid());

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithHomeruleAndTags_AddsTagsSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var tags = new List<string> { "speed-mode", "2-players" };
        var createdBy = Guid.NewGuid();

        var command = new AddDocumentToSharedGameCommand(
            SharedGameId: gameId,
            PdfDocumentId: pdfId,
            DocumentType: SharedGameDocumentType.Homerule,
            Version: "1.0",
            Tags: tags,
            SetAsActive: false,
            CreatedBy: createdBy);

        // Add PDF document to context so existence check passes
        _context.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "test.pdf",
            FilePath = "/uploads/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = createdBy,
            UploadedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var game = CreateTestGame(gameId);
        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _versioningServiceMock
            .Setup(s => s.ValidateVersionDoesNotExistAsync(
                It.IsAny<Guid>(),
                It.IsAny<SharedGameDocumentType>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SharedGameDocument? capturedDocument = null;
        _documentRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGameDocument>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGameDocument, CancellationToken>((d, _) => capturedDocument = d)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedDocument);
        Assert.Equal(2, capturedDocument.Tags.Count);
        Assert.Contains("speed-mode", capturedDocument.Tags);
        Assert.Contains("2-players", capturedDocument.Tags);
    }

    private static SharedGame CreateTestGame(Guid gameId)
    {
        // Use reflection to create a game with specific ID
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.0m,
            averageRating: 7.0m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid());

        return game;
    }
}
