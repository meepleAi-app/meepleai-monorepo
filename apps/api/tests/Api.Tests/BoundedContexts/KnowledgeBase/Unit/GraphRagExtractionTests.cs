using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

/// <summary>
/// Tests for Graph RAG entity extraction integration in the PDF processing pipeline.
/// Verifies that IEntityExtractor is invoked correctly, skipped when absent, and non-blocking on failure.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GraphRagExtractionTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();
    private readonly Mock<IPdfTableExtractor> _tableExtractorMock = new();
    private readonly Mock<ITextChunkingService> _chunkingServiceMock = new();
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IEntityExtractor> _entityExtractorMock = new();
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly ILogger<PdfProcessingPipelineService> _logger =
        NullLogger<PdfProcessingPipelineService>.Instance;

    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public GraphRagExtractionTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"GraphRagPipelineTest_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        // Default: graph-traversal feature flag enabled so entity extraction runs
        _featureFlagServiceMock
            .Setup(f => f.IsEnabledAsync("rag.enhancement.graph-traversal", null))
            .ReturnsAsync(true);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessAsync_WithEntityExtractor_CallsExtractAndSavesRelations()
    {
        // Arrange
        SeedPdfDocument("Uploading");
        var longText = new string('A', 300); // >= 200 char threshold
        SetupExtractorToReturn(longText, 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        var extractionResult = new EntityExtractionResult(
        [
            new ExtractedRelation("Catan", "Game", "HasMechanic", "Trading", "Mechanic", 0.8f),
            new ExtractedRelation("Catan", "Game", "HasComponent", "Hex Tiles", "Component", 0.8f)
        ], 3);

        _entityExtractorMock
            .Setup(e => e.ExtractEntitiesAsync(
                _gameId, It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(extractionResult);

        var sut = CreatePipelineService(withEntityExtractor: true);

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert
        _entityExtractorMock.Verify(
            e => e.ExtractEntitiesAsync(
                _gameId, It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var savedRelations = await _db.GameEntityRelations.ToListAsync();
        Assert.Equal(2, savedRelations.Count);
        Assert.All(savedRelations, r =>
        {
            Assert.Equal(_gameId, r.GameId);
            Assert.NotEqual(Guid.Empty, r.Id);
        });

        // Verify PDF reaches Ready state
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        Assert.Equal("Ready", doc!.ProcessingState);
    }

    [Fact]
    public async Task ProcessAsync_WithoutEntityExtractor_DoesNotThrow()
    {
        // Arrange
        SeedPdfDocument("Uploading");
        SetupExtractorToReturn(new string('A', 300), 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        var sut = CreatePipelineService(withEntityExtractor: false);

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: PDF should reach Ready state
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        Assert.Equal("Ready", doc!.ProcessingState);

        // No entity relations saved
        var relations = await _db.GameEntityRelations.ToListAsync();
        Assert.Empty(relations);
    }

    [Fact]
    public async Task ProcessAsync_WhenEntityExtractionThrows_ContinuesProcessing()
    {
        // Arrange
        SeedPdfDocument("Uploading");
        SetupExtractorToReturn(new string('A', 300), 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        _entityExtractorMock
            .Setup(e => e.ExtractEntitiesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        var sut = CreatePipelineService(withEntityExtractor: true);

        // Act — should NOT throw, pipeline continues
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: PDF should still reach Ready state despite Graph RAG failure
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        Assert.Equal("Ready", doc!.ProcessingState);
    }

    [Fact]
    public async Task ProcessAsync_WithShortText_SkipsEntityExtraction()
    {
        // Arrange — text shorter than 200 chars
        SeedPdfDocument("Uploading");
        SetupExtractorToReturn("Short text.", 1);
        SetupChunkingToReturn(1);
        SetupEmbeddingsToReturn(1);
        SetupBlobStorageToReturn();

        var sut = CreatePipelineService(withEntityExtractor: true);

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: Entity extractor should NOT be called
        _entityExtractorMock.Verify(
            e => e.ExtractEntitiesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyExtractionResult_DoesNotSaveRelations()
    {
        // Arrange
        SeedPdfDocument("Uploading");
        SetupExtractorToReturn(new string('A', 300), 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        _entityExtractorMock
            .Setup(e => e.ExtractEntitiesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult([], 0));

        var sut = CreatePipelineService(withEntityExtractor: true);

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: no relations saved
        var relations = await _db.GameEntityRelations.ToListAsync();
        Assert.Empty(relations);

        // But PDF still completes
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        Assert.Equal("Ready", doc!.ProcessingState);
    }

    [Fact]
    public async Task ProcessAsync_TruncatesTextTo8000Chars()
    {
        // Arrange
        SeedPdfDocument("Uploading");
        var longText = new string('A', 15000); // Much longer than 8000
        SetupExtractorToReturn(longText, 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        _entityExtractorMock
            .Setup(e => e.ExtractEntitiesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.Is<string>(s => s.Length == 8000),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityExtractionResult([], 0));

        var sut = CreatePipelineService(withEntityExtractor: true);

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: called with truncated text (8000 chars)
        _entityExtractorMock.Verify(
            e => e.ExtractEntitiesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.Is<string>(s => s.Length == 8000),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private PdfProcessingPipelineService CreatePipelineService(bool withEntityExtractor)
    {
        return new PdfProcessingPipelineService(
            _db,
            _pdfTextExtractorMock.Object,
            _tableExtractorMock.Object,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _blobStorageServiceMock.Object,
            _timeProvider,
            _logger,
            raptorIndexer: null,
            entityExtractor: withEntityExtractor ? _entityExtractorMock.Object : null,
            vectorStore: null,
            featureFlagService: _featureFlagServiceMock.Object);
    }

    private PdfDocumentEntity SeedPdfDocument(string state)
    {
        var pdfDoc = new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            GameId = _gameId,
            FileName = "Catan Rules.pdf",
            FilePath = "/fake/path/test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = state,
            UploadedAt = DateTime.UtcNow
        };
        _db.PdfDocuments.Add(pdfDoc);
        _db.SaveChanges();
        return pdfDoc;
    }

    private void SetupBlobStorageToReturn()
    {
        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF header
    }

    private void SetupExtractorToReturn(string fullText, int pageCount)
    {
        var pageChunks = Enumerable.Range(1, pageCount)
            .Select(p => new PageTextChunk(
                PageNumber: p,
                Text: fullText,
                CharStartIndex: (p - 1) * 50,
                CharEndIndex: p * 50))
            .ToList();

        var result = PagedTextExtractionResult.CreateSuccess(
            pageChunks: pageChunks,
            totalPages: pageCount,
            totalCharacters: fullText.Length,
            ocrTriggered: false);

        _pdfTextExtractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _tableExtractorMock
            .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StructuredContentResult.CreateFailure("Not applicable"));
    }

    private void SetupChunkingToReturn(int chunkCount)
    {
        var chunkInputs = Enumerable.Range(0, chunkCount).Select(i =>
            new DocumentChunkInput
            {
                Text = $"Chunk {i} content for testing purposes.",
                Page = i + 1,
                CharStart = i * 100,
                CharEnd = (i + 1) * 100
            }).ToList();

        _chunkingServiceMock
            .Setup(c => c.PrepareForEmbedding(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(chunkInputs);
    }

    private void SetupEmbeddingsToReturn(int count)
    {
        var embeddings = Enumerable.Range(0, count)
            .Select(_ => new float[] { 0.1f, 0.2f, 0.3f })
            .ToList();

        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = embeddings });
    }
}
