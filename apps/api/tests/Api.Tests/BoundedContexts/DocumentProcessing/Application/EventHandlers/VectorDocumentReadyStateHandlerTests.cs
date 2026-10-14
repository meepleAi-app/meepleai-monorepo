using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.IntegrationEvents;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Unit tests for VectorDocumentReadyStateHandler.
/// Issue #5237: Verifies compensating state update for PDF documents
/// when vector indexing completes via integration event.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class VectorDocumentReadyStateHandlerTests
{
    private readonly Mock<ILogger<VectorDocumentReadyStateHandler>> _logger;

    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public VectorDocumentReadyStateHandlerTests()
    {
        _logger = new Mock<ILogger<VectorDocumentReadyStateHandler>>();
    }

    private VectorDocumentReadyIntegrationEvent CreateEvent(string currentState = "Processing") =>
        new()
        {
            DocumentId = _documentId,
            GameId = _gameId,
            ChunkCount = 247,
            PdfDocumentId = _pdfDocumentId,
            UploadedByUserId = _userId,
            FileName = "test-rulebook.pdf",
            CurrentProcessingState = currentState
        };

    [Fact]
    public async Task Handle_StateIsReady_SkipsUpdate()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new VectorDocumentReadyStateHandler(dbContext, _logger.Object);
        var evt = CreateEvent("Ready");

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert — no DB query should occur for already-Ready state
        var entity = await dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == _pdfDocumentId);
        entity.Should().BeNull(); // nothing was created or updated
    }

    [Fact]
    public async Task Handle_StateIsFailed_SkipsUpdate()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new VectorDocumentReadyStateHandler(dbContext, _logger.Object);
        var evt = CreateEvent("Failed");

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        var entity = await dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == _pdfDocumentId);
        entity.Should().BeNull();
    }

    [Fact]
    public async Task Handle_StateIsProcessing_PdfNotFound_DoesNotThrow()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new VectorDocumentReadyStateHandler(dbContext, _logger.Object);
        var evt = CreateEvent("Processing");

        // Act & Assert — should not throw when PDF entity doesn't exist
        await sut.Invoking(h => h.Handle(evt, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    /// <summary>
    /// Il caso per cui la compensazione esiste: l'indice è scritto (gli embedding ci sono), ma il
    /// PDF è rimasto in <c>Indexing</c> perché la pipeline si è fermata prima di finalizzare.
    /// </summary>
    [Fact]
    public async Task Handle_StateIsProcessing_PdfFound_UpdatesToReady()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        var pdfEntity = new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            UploadedByUserId = _userId,
            FileName = "test-rulebook.pdf",
            FilePath = "/test/test-rulebook.pdf",
            FileSizeBytes = 1024,
            ProcessingState = "Indexing",
        };
        dbContext.PdfDocuments.Add(pdfEntity);
        dbContext.PgVectorEmbeddings.Add(AnEmbeddingOf(_documentId));
        await dbContext.SaveChangesAsync();

        var sut = new VectorDocumentReadyStateHandler(dbContext, _logger.Object);
        var evt = CreateEvent("Indexing");

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        var updated = await dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == _pdfDocumentId);
        updated.Should().NotBeNull();
        updated!.ProcessingState.Should().Be("Ready");
        updated.ProcessingState.Should().Be("Ready");
    }

    /// <summary>
    /// #4339: l'evento arriva in modo sincrono da <c>IPdfIndexingPipeline.IndexAsync</c>, che le
    /// pipeline chiamano PRIMA di scrivere passaggi ed embedding. Un PDF in <c>Indexing</c> senza
    /// embedding non è rimasto indietro: è a metà, e la pipeline lo porterà a <c>Ready</c> da sé.
    /// Portarcelo qui faceva fallire la finalizzazione di ogni caricamento («Cannot transition from
    /// Ready state»).
    /// </summary>
    [Fact]
    public async Task Handle_StateIsIndexing_TheIndexIsNotWrittenYet_LeavesThePdfToThePipeline()
    {
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            UploadedByUserId = _userId,
            FileName = "test-rulebook.pdf",
            FilePath = "/test/test-rulebook.pdf",
            FileSizeBytes = 1024,
            ProcessingState = "Indexing",
        });
        // Un embedding di un ALTRO documento vettoriale non conta.
        dbContext.PgVectorEmbeddings.Add(AnEmbeddingOf(Guid.NewGuid()));
        await dbContext.SaveChangesAsync();
        var sut = new VectorDocumentReadyStateHandler(dbContext, _logger.Object);

        await sut.Handle(CreateEvent("Indexing"), CancellationToken.None);

        var pdf = await dbContext.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == _pdfDocumentId);
        pdf.ProcessingState.Should().Be("Indexing");
    }

    private PgVectorEmbeddingEntity AnEmbeddingOf(Guid vectorDocumentId) => new()
    {
        Id = Guid.NewGuid(),
        VectorDocumentId = vectorDocumentId,
        GameId = _gameId,
        TextContent = "A passage.",
        Vector = new Pgvector.Vector(new[] { 0.1f, 0.2f, 0.3f }),
        Model = "test-model",
        ChunkIndex = 0,
        PageNumber = 1,
    };
}
