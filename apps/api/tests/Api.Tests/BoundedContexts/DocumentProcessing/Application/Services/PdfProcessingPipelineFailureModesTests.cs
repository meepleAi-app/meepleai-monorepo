using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #4442: il caricamento a pezzi non ha più una propria copia della pipeline, e i suoi documenti li
/// elabora <see cref="PdfProcessingPipelineService"/>. I casi di guasto che i test della copia
/// proteggevano (nessun passaggio utile, embedding fallito, vettore non valido) sono portati qui: un
/// guasto deve finire <c>Failed</c>, mai lasciare il documento a metà, e non deve scrivere nulla.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4442")]
public sealed class PdfProcessingPipelineFailureModesTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfTextExtractor> _extractor = new();
    private readonly Mock<IPdfTableExtractor> _tables = new();
    private readonly Mock<IEmbeddingService> _embeddings = new();
    private readonly Mock<IBlobStorageService> _blobs = new();
    private readonly Mock<IAdvancedChunkingService> _advancedChunking = new();
    private readonly Guid _pdfId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public PdfProcessingPipelineFailureModesTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"PdfPipelineFailureModes_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);

        _blobs.Setup(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF
        _extractor.Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                pageChunks: [new PageTextChunk(1, "Setup. Place the tiles.", 0, 23)],
                totalPages: 1,
                totalCharacters: 23,
                ocrTriggered: false,
                structuredElements: [new ExtractedElement("Setup", 1, "Title"), new ExtractedElement("Place the tiles.", 1, "NarrativeText")]));
        _tables.Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StructuredContentResult.CreateFailure("Not applicable"));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ProcessAsync_WhenNoUsableChunksAreProduced_EndsFailed()
    {
        SeedPendingPdf();
        _advancedChunking
            .Setup(s => s.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk>());

        var outcome = await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        outcome.Should().Be(PdfPipelineOutcome.Failed);
        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.ProcessingState.Should().Be("Failed");
        pdf.ProcessingError.Should().Contain("no usable chunks");
        pdf.FailedAtState.Should().Be("Chunking", "la fase in cui il documento si è fermato");
    }

    [Fact]
    public async Task ProcessAsync_WhenTheEmbeddingBatchFails_EndsFailed_NotStuckInFlight()
    {
        SeedPendingPdf();
        SetupTwoChunks();
        _embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = false, ErrorMessage = "embedding service down" });

        var outcome = await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        outcome.Should().Be(PdfPipelineOutcome.Failed);
        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.ProcessingState.Should().Be("Failed", "un guasto di embedding non deve lasciare il documento in Embedding");
        pdf.ProcessingError.Should().Contain("embedding service down");
        pdf.FailedAtState.Should().Be("Embedding");
    }

    [Fact]
    public async Task ProcessAsync_WithAnInvalidVector_EndsFailed_WithoutWritingAnything()
    {
        SeedPendingPdf();
        SetupTwoChunks();
        _embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) => new EmbeddingResult
            {
                Success = true,
                Embeddings = texts.Select(_ => new[] { 0.1f, float.NaN, 0.3f }).ToList(),
            });

        var outcome = await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        outcome.Should().Be(PdfPipelineOutcome.Failed);
        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.ProcessingState.Should().Be("Failed");
        pdf.FailedAtState.Should().Be("Embedding");
        (await _db.TextChunks.CountAsync(tc => tc.PdfDocumentId == _pdfId)).Should().Be(0, "i vettori si controllano prima di ogni scrittura");
        (await _db.PgVectorEmbeddings.CountAsync()).Should().Be(0);
    }

    private PdfProcessingPipelineService CreatePipeline()
    {
        var language = new Mock<ILanguageDetector>();
        language.Setup(l => l.Detect(It.IsAny<string>())).Returns(new LanguageDetectionResult("en", true, 0.95));

        return new PdfProcessingPipelineService(
            _db,
            new Api.Tests.TestHelpers.InMemoryPdfClaimService(_db),
            _extractor.Object,
            _tables.Object,
            Mock.Of<ITextChunkingService>(),
            _embeddings.Object,
            _blobs.Object,
            TimeProvider.System,
            NullLogger<PdfProcessingPipelineService>.Instance,
            language.Object,
            Mock.Of<IChunkTranslationService>(),
            Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.IPdfIndexingPipeline>(),
            advancedChunking: _advancedChunking.Object);
    }

    private void SeedPendingPdf()
    {
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _pdfId,
            PrivateGameId = _gameId,
            FileName = "rules.pdf",
            FilePath = "pdfs/game/file_rules.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Pending",
            UploadedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    private void SetupTwoChunks()
    {
        var parent = HierarchicalChunk.CreateParent(
            "Setup section content",
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "Title", DocumentId = _pdfId });
        var child = HierarchicalChunk.CreateChild(
            "Place the tiles.",
            level: 2,
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "NarrativeText", DocumentId = _pdfId },
            parent.Id);
        _advancedChunking
            .Setup(s => s.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { parent, child });
    }
}
