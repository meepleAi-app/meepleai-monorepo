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
/// Tests for RAPTOR hierarchical indexing integration in the PDF processing pipeline.
/// Verifies that RAPTOR is invoked correctly, skipped when absent, and non-blocking on failure.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RaptorPipelineIntegrationTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();
    private readonly Mock<IPdfTableExtractor> _tableExtractorMock = new();
    private readonly Mock<ITextChunkingService> _chunkingServiceMock = new();
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IRaptorIndexer> _raptorIndexerMock = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly ILogger<PdfProcessingPipelineService> _logger =
        NullLogger<PdfProcessingPipelineService>.Instance;

    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public RaptorPipelineIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"RaptorPipelineTest_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessAsync_WithRaptorIndexer_AndMoreThan3Chunks_CallsBuildTreeAsync()
    {
        // Arrange
        var pdfDoc = SeedPdfDocument("Uploading");
        SetupExtractorToReturn("chunk1 text. chunk2 text. chunk3 text. chunk4 text.", 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        var raptorResult = new RaptorTreeResult(
            TotalNodes: 2, Levels: 2,
            Summaries: new List<RaptorSummaryNode>
            {
                new(TreeLevel: 1, ClusterIndex: 0, SummaryText: "Summary of cluster 0", SourceChunkCount: 2),
                new(TreeLevel: 1, ClusterIndex: 1, SummaryText: "Summary of cluster 1", SourceChunkCount: 2)
            });

        _raptorIndexerMock
            .Setup(r => r.BuildTreeAsync(
                _pdfDocumentId, _gameId,
                It.IsAny<IReadOnlyList<string>>(), 3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(raptorResult);

        var sut = CreatePipelineService(withRaptor: true);

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert
        _raptorIndexerMock.Verify(
            r => r.BuildTreeAsync(
                _pdfDocumentId, _gameId,
                It.Is<IReadOnlyList<string>>(list => list.Count == 4),
                3,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify summaries were saved
        var savedSummaries = await _db.RaptorSummaries.ToListAsync();
        Assert.Equal(2, savedSummaries.Count);
        Assert.All(savedSummaries, s =>
        {
            Assert.Equal(_pdfDocumentId, s.PdfDocumentId);
            Assert.Equal(_gameId, s.GameId);
        });
    }

    [Fact]
    public async Task ProcessAsync_WithoutRaptorIndexer_DoesNotThrow()
    {
        // Arrange
        var pdfDoc = SeedPdfDocument("Uploading");
        SetupExtractorToReturn("chunk1. chunk2. chunk3. chunk4.", 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        var sut = CreatePipelineService(withRaptor: false);

        // Act — should complete without error
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: PDF should reach Ready state
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        Assert.Equal("Ready", doc!.ProcessingState);

        // No RAPTOR summaries saved
        var summaries = await _db.RaptorSummaries.ToListAsync();
        Assert.Empty(summaries);
    }

    [Fact]
    public async Task ProcessAsync_WhenBuildTreeAsyncThrows_ContinuesProcessing()
    {
        // Arrange
        var pdfDoc = SeedPdfDocument("Uploading");
        SetupExtractorToReturn("chunk1. chunk2. chunk3. chunk4.", 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        _raptorIndexerMock
            .Setup(r => r.BuildTreeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        var sut = CreatePipelineService(withRaptor: true);

        // Act — should NOT throw, pipeline continues
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: PDF should still reach Ready state despite RAPTOR failure
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        Assert.Equal("Ready", doc!.ProcessingState);
    }

    [Fact]
    public async Task ProcessAsync_With3OrFewerChunks_SkipsRaptor()
    {
        // Arrange
        var pdfDoc = SeedPdfDocument("Uploading");
        SetupExtractorToReturn("chunk1. chunk2. chunk3.", 3);
        SetupChunkingToReturn(3); // Exactly 3 chunks — threshold not met
        SetupEmbeddingsToReturn(3);
        SetupBlobStorageToReturn();

        var sut = CreatePipelineService(withRaptor: true);

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: RAPTOR should NOT be called
        _raptorIndexerMock.Verify(
            r => r.BuildTreeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithNullGameId_PassesGuidEmpty()
    {
        // Arrange — seed PDF with null GameId
        var pdfDoc = new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            GameId = null,
            FileName = "test.pdf",
            FilePath = "/fake/path/test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Uploading",
            UploadedAt = DateTime.UtcNow
        };
        _db.PdfDocuments.Add(pdfDoc);
        _db.SaveChanges();

        SetupExtractorToReturn("chunk1. chunk2. chunk3. chunk4.", 4);
        SetupChunkingToReturn(4);
        SetupEmbeddingsToReturn(4);
        SetupBlobStorageToReturn();

        _raptorIndexerMock
            .Setup(r => r.BuildTreeAsync(
                _pdfDocumentId, Guid.Empty,
                It.IsAny<IReadOnlyList<string>>(), 3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RaptorTreeResult(0, 0, new List<RaptorSummaryNode>()));

        var sut = CreatePipelineService(withRaptor: true);

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: called with Guid.Empty for null GameId
        _raptorIndexerMock.Verify(
            r => r.BuildTreeAsync(
                _pdfDocumentId, Guid.Empty,
                It.IsAny<IReadOnlyList<string>>(), 3,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private PdfProcessingPipelineService CreatePipelineService(bool withRaptor)
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
            withRaptor ? _raptorIndexerMock.Object : null);
    }

    private PdfDocumentEntity SeedPdfDocument(string state)
    {
        var pdfDoc = new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            GameId = _gameId,
            FileName = "test.pdf",
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
                Text: $"chunk{p} text",
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
