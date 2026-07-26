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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Issue #3281 (Task 2): wires <see cref="HeadingAwareChunker.BuildAsync"/> into
/// <see cref="PdfProcessingPipelineService"/> — the Quartz/stale-recovery + shared-game
/// upload ingest path. Covers the English (no translation) production of heading-bearing
/// chunks, and the non-English translation branch REGRESSION guard: translated chunks
/// must NOT copy <c>origChunk.Id</c> (a non-empty stable Guid post-producer-swap), else
/// <c>db.TextChunks.AddRange</c> throws an EF identity-map collision and every
/// non-English PDF is marked Failed.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfProcessingPipelineHeadingAwareTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();
    private readonly Mock<IPdfTableExtractor> _tableExtractorMock = new();
    private readonly Mock<ITextChunkingService> _chunkingServiceMock = new();
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IAdvancedChunkingService> _advancedChunkingMock = new();
    private readonly Mock<IChunkTranslationService> _chunkTranslationServiceMock = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly ILogger<PdfProcessingPipelineService> _logger =
        NullLogger<PdfProcessingPipelineService>.Instance;

    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public PdfProcessingPipelineHeadingAwareTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"PdfPipelineHeadingAwareTest_{Guid.NewGuid()}")
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
    public async Task ProcessAsync_English_ProducesHeadingBearingChunksWithHierarchy()
    {
        // Arrange
        SeedPdfDocument();
        SetupExtractorToReturn("chunk1. chunk2.", 2);
        SetupBlobStorageToReturn();
        SetupHierarchicalChunking(out var parentId);
        SetupEmbeddingsToReturn(2);

        var sut = CreatePipelineService(languageCode: "en");

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: pipeline reaches Ready
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        doc!.ProcessingState.Should().Be("Ready");

        var savedChunks = await _db.TextChunks
            .Where(tc => tc.PdfDocumentId == _pdfDocumentId)
            .ToListAsync();

        savedChunks.Should().HaveCount(2);
        savedChunks.Should().Contain(c => c.Heading == "Setup" && c.Level == 0 && c.ParentChunkId == null);
        savedChunks.Should().Contain(c => c.Heading == "Setup" && c.Level == 2 && c.ParentChunkId == parentId);

        // English document: translation branch must not run.
        _chunkTranslationServiceMock.Verify(
            t => t.TranslateChunksAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_NonEnglish_TranslatedChunksGetFreshIds_NoPrimaryKeyCollision()
    {
        // Arrange — regression guard for the PK-collision fix (plan Task 2, CRITICAL note):
        // after the producer swap, origChunk.Id is a non-empty stable Guid (from
        // HierarchicalChunkMapper). If the translation branch copied that Id onto the
        // translated DocumentChunkInput, db.TextChunks.AddRange would throw an EF
        // identity-map InvalidOperationException (uncaught — only DbUpdateConcurrencyException
        // is handled) and the PDF would be marked Failed for every non-English document.
        SeedPdfDocument();
        SetupExtractorToReturn("chunk1. chunk2.", 2);
        SetupBlobStorageToReturn();
        SetupHierarchicalChunking(out var parentId);
        SetupTranslation();
        SetupEmbeddingsToReturn(4); // 2 originals + 2 EN translations

        var sut = CreatePipelineService(languageCode: "it");

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: pipeline completes WITHOUT the PDF being marked Failed.
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        doc!.ProcessingState.Should().Be("Ready");
        doc.ProcessingError.Should().BeNull();

        var savedChunks = await _db.TextChunks
            .Where(tc => tc.PdfDocumentId == _pdfDocumentId)
            .ToListAsync();

        // 2 originals + 2 EN translations, all with DISTINCT Ids (no PK collision).
        savedChunks.Should().HaveCount(4);
        savedChunks.Select(c => c.Id).Should().OnlyHaveUniqueItems();

        // Translated rows still carry the hierarchy fields copied from origChunk.
        savedChunks.Should().Contain(c => c.Heading == "Setup" && c.Level == 2 && c.ParentChunkId == parentId);
    }

    [Fact]
    public async Task ProcessAsync_OnReady_StampsCurrentIndexerVersion()
    {
        // Finding 2 (#3269 review): a freshly-processed PDF must end Ready stamped with the
        // current indexer version, so that `IndexerVersion == null` denotes only true
        // pre-versioning legacy — otherwise the bulk re-index selector redundantly
        // re-processes fresh (already heading-aware) documents on every run.
        SeedPdfDocument();
        SetupExtractorToReturn("chunk1. chunk2.", 2);
        SetupBlobStorageToReturn();
        SetupHierarchicalChunking(out _);
        SetupEmbeddingsToReturn(2);

        var sut = CreatePipelineService(languageCode: "en");

        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        doc!.ProcessingState.Should().Be("Ready");
        doc.IndexerVersion.Should().Be(IndexerVersionRegistry.Current.Version);
    }

    private PdfProcessingPipelineService CreatePipelineService(string languageCode)
    {
        var languageDetectorMock = new Mock<ILanguageDetector>();
        languageDetectorMock
            .Setup(l => l.Detect(It.IsAny<string>()))
            .Returns(new LanguageDetectionResult(languageCode, true, 0.95));

        return new PdfProcessingPipelineService(
            _db,
            new Api.Tests.TestHelpers.InMemoryPdfClaimService(_db),
            _pdfTextExtractorMock.Object,
            _tableExtractorMock.Object,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _blobStorageServiceMock.Object,
            _timeProvider,
            _logger,
            languageDetectorMock.Object,
            _chunkTranslationServiceMock.Object,
            Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.IPdfIndexingPipeline>(),
            advancedChunking: _advancedChunkingMock.Object);
    }

    private PdfDocumentEntity SeedPdfDocument()
    {
        // Use PrivateGameId so PdfGameIdResolver returns _gameId directly without a
        // games-table lookup (mirrors RaptorPipelineIntegrationTests.SeedPdfDocument).
        var pdfDoc = new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            PrivateGameId = _gameId,
            FileName = "test.pdf",
            FilePath = "/fake/path/test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Pending",
            UploadedAt = DateTime.UtcNow
        };
        _db.PdfDocuments.Add(pdfDoc);
        _db.SaveChanges();
        return pdfDoc;
    }

    private void SetupBlobStorageToReturn()
    {
        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        var structuredElements = new List<ExtractedElement>
        {
            new("Setup", 1, "Title"),
            new("Place the tiles.", 1, "NarrativeText")
        };

        var result = PagedTextExtractionResult.CreateSuccess(
            pageChunks: pageChunks,
            totalPages: pageCount,
            totalCharacters: fullText.Length,
            ocrTriggered: false,
            structuredElements: structuredElements);

        _pdfTextExtractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _tableExtractorMock
            .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StructuredContentResult.CreateFailure("Not applicable"));
    }

    /// <summary>
    /// Mocks <see cref="IAdvancedChunkingService.ChunkDocumentAsync"/> to return a parent
    /// (Level 0, Heading "Setup") + child (Level 2, ParentChunkId=parent, Heading "Setup") —
    /// the minimal hierarchy shape <c>HeadingAwareChunker.BuildAsync</c> /
    /// <c>HierarchicalChunkMapper</c> produce (mirrors HeadingAwareChunkerTests).
    /// </summary>
    private void SetupHierarchicalChunking(out Guid parentId)
    {
        var parent = HierarchicalChunk.CreateParent(
            "Setup section content",
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "Title", DocumentId = _pdfDocumentId });
        var child = HierarchicalChunk.CreateChild(
            "Place the tiles.",
            level: 2,
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "NarrativeText", DocumentId = _pdfDocumentId },
            parent.Id);

        _advancedChunkingMock
            .Setup(s => s.ChunkDocumentAsync(
                It.IsAny<ExtractedDocument>(),
                It.IsAny<ChunkingConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { parent, child });

        parentId = Guid.ParseExact(parent.Id, "N");
    }

    private void SetupTranslation()
    {
        _chunkTranslationServiceMock
            .Setup(t => t.TranslateChunksAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> texts, string src, string tgt, CancellationToken _) =>
                texts.Select((text, idx) => new TranslatedChunk(idx, text, $"EN: {text}", src, tgt)).ToList());
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
