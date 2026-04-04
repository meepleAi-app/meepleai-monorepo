using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Handlers;

/// <summary>
/// Unit tests for LaunchAdminPdfProcessingCommandHandler.
/// Issue #4673: Verifies SharedGameId resolution, IDOR check, priority=Admin assignment, pipeline dispatch.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class LaunchAdminPdfProcessingCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IMediator> _mockMediator;
    private readonly LaunchAdminPdfProcessingCommandHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();

    public LaunchAdminPdfProcessingCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockMediator = new Mock<IMediator>();

        _handler = new LaunchAdminPdfProcessingCommandHandler(
            _dbContext,
            _mockMediator.Object,
            NullLogger<LaunchAdminPdfProcessingCommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    private void SetupDefaultPipelineMocks()
    {
        _mockMediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        _mockMediator.Setup(m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExtractPdfTextResultDto.CreateSuccess(1000, 5));
        _mockMediator.Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexingResultDto.CreateSuccess("vec-doc-1", 10, DateTime.UtcNow));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Happy-path: direct GameId (no SharedGameId resolution required)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithDirectGameId_SetsPriorityAdminAndReturnsResult()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Games.Add(new GameEntity { Id = gameId, Name = "Gloomhaven" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = "Normal"
        });
        await _dbContext.SaveChangesAsync();
        SetupDefaultPipelineMocks();

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: gameId, PdfDocumentId: pdfId, LaunchedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("processing");
        result.Priority.Should().Be("Admin");
        result.PdfDocumentId.Should().Be(pdfId);
        result.GameId.Should().Be(gameId);
    }

    [Fact]
    public async Task Handle_WithDirectGameId_SetsPriorityAdminInDatabase()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Games.Add(new GameEntity { Id = gameId, Name = "Gloomhaven" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "rulebook.pdf",
            FilePath = "/uploads/rulebook.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = "Normal"
        });
        await _dbContext.SaveChangesAsync();
        SetupDefaultPipelineMocks();

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: gameId, PdfDocumentId: pdfId, LaunchedByUserId: UserId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var pdf = await _dbContext.PdfDocuments.FindAsync(pdfId);
        pdf!.ProcessingPriority.Should().Be("Admin");
    }

    // ────────────────────────────────────────────────────────────────────────
    // SharedGameId resolution: wizard passes SharedGameId, handler resolves to Game.Id
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithSharedGameId_ResolvesToActualGameId()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var actualGameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        // Game entity links SharedGameId → actual Game.Id
        _dbContext.Games.Add(new GameEntity
        {
            Id = actualGameId,
            Name = "Catan",
            SharedGameId = sharedGameId
        });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = actualGameId,   // PDF references actual Game.Id
            FileName = "catan.pdf",
            FilePath = "/uploads/catan.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = "Normal"
        });
        await _dbContext.SaveChangesAsync();
        SetupDefaultPipelineMocks();

        // Command uses SharedGameId (as the wizard does)
        var command = new LaunchAdminPdfProcessingCommand(
            GameId: sharedGameId, PdfDocumentId: pdfId, LaunchedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: handler resolved SharedGameId and found the PDF
        result.Status.Should().Be("processing");
        result.Priority.Should().Be("Admin");

        var pdf = await _dbContext.PdfDocuments.FindAsync(pdfId);
        pdf!.ProcessingPriority.Should().Be("Admin");
    }

    // ────────────────────────────────────────────────────────────────────────
    // SharedGameId resolution: no Game exists with that SharedGameId → NotFoundException
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithSharedGameId_WhenNoGameExists_ThrowsNotFoundException()
    {
        // Arrange: DB is empty — no Game matches the passed SharedGameId
        var nonExistentSharedGameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: nonExistentSharedGameId, PdfDocumentId: pdfId, LaunchedByUserId: UserId);

        // Act & Assert: handler falls through to PDF lookup with unresolved ID → NotFoundException
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // IDOR prevention: PDF belongs to a different game
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPdfBelongsToDifferentGame_ThrowsNotFoundException()
    {
        // Arrange
        var targetGameId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Games.Add(new GameEntity { Id = targetGameId, Name = "Game A" });
        _dbContext.Games.Add(new GameEntity { Id = otherGameId, Name = "Game B" });
        // PDF belongs to otherGameId
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = otherGameId,
            FileName = "other.pdf",
            FilePath = "/uploads/other.pdf",
            UploadedByUserId = UserId
        });
        await _dbContext.SaveChangesAsync();

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: targetGameId, PdfDocumentId: pdfId, LaunchedByUserId: UserId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // NotFoundException: PDF does not exist at all
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPdfNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _dbContext.Games.Add(new GameEntity { Id = gameId, Name = "Pandemic" });
        await _dbContext.SaveChangesAsync();

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: gameId, PdfDocumentId: Guid.NewGuid(), LaunchedByUserId: UserId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Pipeline dispatch: both ExtractPdfTextCommand and IndexPdfCommand are sent
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSuccessful_DispatchesBothPipelineCommands()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Games.Add(new GameEntity { Id = gameId, Name = "Spirit Island" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "spirit-island.pdf",
            FilePath = "/uploads/spirit-island.pdf",
            UploadedByUserId = UserId
        });
        await _dbContext.SaveChangesAsync();
        SetupDefaultPipelineMocks();

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: gameId, PdfDocumentId: pdfId, LaunchedByUserId: UserId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: enqueue + both pipeline commands were dispatched
        _mockMediator.Verify(
            m => m.Send(It.Is<EnqueuePdfCommand>(c => c.PdfDocumentId == pdfId && c.Priority == 100), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockMediator.Verify(
            m => m.Send(It.Is<ExtractPdfTextCommand>(c => c.PdfId == pdfId), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockMediator.Verify(
            m => m.Send(It.Is<IndexPdfCommand>(c => c.PdfId == pdfId.ToString()), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Enqueue: duplicate job is tolerated (ConflictException caught)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenEnqueueThrowsConflict_StillProcessesPipeline()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Games.Add(new GameEntity { Id = gameId, Name = "Everdell" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "everdell.pdf",
            FilePath = "/uploads/everdell.pdf",
            UploadedByUserId = UserId
        });
        await _dbContext.SaveChangesAsync();

        // Enqueue throws ConflictException (already queued)
        _mockMediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Already queued"));
        _mockMediator.Setup(m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExtractPdfTextResultDto.CreateSuccess(1000, 5));
        _mockMediator.Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexingResultDto.CreateSuccess("vec-doc-1", 10, DateTime.UtcNow));

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: gameId, PdfDocumentId: pdfId, LaunchedByUserId: UserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: pipeline still runs despite enqueue conflict
        result.Status.Should().Be("processing");
        _mockMediator.Verify(
            m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Null command guard
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Pipeline failure propagates
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPipelineCommandThrows_ExceptionPropagates()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext.Games.Add(new GameEntity { Id = gameId, Name = "Arkham Horror" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "arkham.pdf",
            FilePath = "/uploads/arkham.pdf",
            UploadedByUserId = UserId
        });
        await _dbContext.SaveChangesAsync();

        _mockMediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        _mockMediator.Setup(m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Extraction service unavailable"));

        var command = new LaunchAdminPdfProcessingCommand(
            GameId: gameId, PdfDocumentId: pdfId, LaunchedByUserId: UserId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Extraction service unavailable");
    }
}
