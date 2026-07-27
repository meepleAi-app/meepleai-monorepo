using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Regression guard for the pipeline-ordering bug (B3): <see cref="PdfProcessingPipelineService"/>
/// used to call <c>IndexInVectorStoreAsync</c> BEFORE <c>SaveTextChunksAsync</c>. The vector-store
/// indexing step denormalizes <c>role_tags</c> + <c>source_chunk_id</c> onto the pgvector rows by
/// reading the freshly-saved <c>text_chunks</c> (keyed by <c>ChunkIndex</c>) — but with the old
/// ordering those rows did not exist yet, so every pgvector row was born with
/// <c>RoleTags == 0</c> (None) and <c>SourceChunkId == null</c>. That silently disabled the
/// vector-arm role-match boost (<c>FusionSignals.ComputeRoleMatchBoost</c>) corpus-wide.
///
/// This test captures the <see cref="Embedding"/> batch handed to
/// <see cref="IVectorStoreAdapter.IndexBatchAsync"/> — the correct seam (NOT EF PgVectorEmbeddings) —
/// and asserts each row's <c>RoleTags</c> / <c>SourceChunkId</c> match the persisted
/// <see cref="TextChunkEntity"/> at the same <c>ChunkIndex</c>. RED before the swap (all 0 / null),
/// GREEN after.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfProcessingPipelineRoleTagsTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();
    private readonly Mock<IPdfTableExtractor> _tableExtractorMock = new();
    private readonly Mock<ITextChunkingService> _chunkingServiceMock = new();
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IAdvancedChunkingService> _advancedChunkingMock = new();
    private readonly Mock<IChunkTranslationService> _chunkTranslationServiceMock = new();
    private readonly Mock<IRoleClassifierService> _roleClassifierMock = new();
    private readonly Mock<IVectorStoreAdapter> _vectorStoreMock = new();
    private readonly Mock<IPdfIndexingPipeline> _indexingPipelineMock = new();

    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public PdfProcessingPipelineRoleTagsTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessAsync_DenormalizesRoleTagsAndSourceChunkId_OntoPgvectorRows()
    {
        // Arrange
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();

        List<Embedding>? captured = null;
        _vectorStoreMock
            .Setup(v => v.IndexBatchAsync(It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .Callback((List<Embedding> batch, CancellationToken _) => captured = batch)
            .Returns(Task.CompletedTask);

        var sut = CreatePipeline();

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: pipeline ran to completion (guards against a vacuous pass on an early failure).
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        doc!.ProcessingState.Should().Be("Ready");

        captured.Should().NotBeNull("the pipeline must hand the pgvector rows to IVectorStoreAdapter.IndexBatchAsync");

        var persistedByIndex = await _db.TextChunks
            .Where(tc => tc.PdfDocumentId == _pdfDocumentId)
            .ToDictionaryAsync(tc => tc.ChunkIndex);

        captured!.Should().HaveCount(persistedByIndex.Count);

        // The classifier tagged every chunk with a NON-None role. Each pgvector row must carry the
        // SAME role + reference the persisted text_chunk id, matched by ChunkIndex. Pre-fix the
        // denormalization lookup was empty (text_chunks not saved yet) so all rows were 0 / null.
        foreach (var emb in captured!)
        {
            persistedByIndex.Should().ContainKey(emb.ChunkIndex);
            var tc = persistedByIndex[emb.ChunkIndex];
            emb.RoleTags.Should().Be((int)tc.RoleTags,
                "each pgvector row must denormalize the persisted text_chunk role_tags at the same ChunkIndex");
            emb.SourceChunkId.Should().Be(tc.Id,
                "each pgvector row must reference the persisted text_chunk id at the same ChunkIndex");
        }

        // Explicit bug signal: no row may be left at the default (roleTags=0 / sourceChunkId=null).
        captured!.Should().OnlyContain(e => e.RoleTags != 0);
        captured!.Should().OnlyContain(e => e.SourceChunkId != null);
    }

    private PdfProcessingPipelineService CreatePipeline()
    {
        var languageDetectorMock = new Mock<ILanguageDetector>();
        languageDetectorMock
            .Setup(l => l.Detect(It.IsAny<string>()))
            .Returns(new LanguageDetectionResult("en", true, 0.95));

        return new PdfProcessingPipelineService(
            _db,
            new InMemoryPdfClaimService(_db),
            _pdfTextExtractorMock.Object,
            _tableExtractorMock.Object,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _blobStorageServiceMock.Object,
            TimeProvider.System,
            NullLogger<PdfProcessingPipelineService>.Instance,
            languageDetectorMock.Object,
            _chunkTranslationServiceMock.Object,
            _indexingPipelineMock.Object,
            vectorStore: _vectorStoreMock.Object,
            roleClassifier: _roleClassifierMock.Object,
            advancedChunking: _advancedChunkingMock.Object);
    }

    private void SeedPdfDocument()
    {
        // PrivateGameId so PdfGameIdResolver returns _gameId directly (no games-table lookup),
        // mirroring PdfProcessingPipelineHeadingAwareTests.SeedPdfDocument.
        _db.PdfDocuments.Add(new PdfDocumentEntity
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
        });
        _db.SaveChanges();
    }

    private void SetupBlobStorage()
    {
        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF header
    }

    private void SetupExtractor(int pageCount)
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
            totalCharacters: pageChunks.Sum(pc => pc.Text.Length),
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
    /// Parent (Level 0) + two children (Level 2), each with distinct body text so the resulting
    /// chunk inputs are unambiguous. Mirrors the minimal hierarchy shape
    /// <c>HeadingAwareChunker.BuildAsync</c> / <c>HierarchicalChunkMapper</c> produce.
    /// </summary>
    private void SetupHierarchicalChunking()
    {
        var parent = HierarchicalChunk.CreateParent(
            "Setup section content",
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "Title", DocumentId = _pdfDocumentId });
        var child1 = HierarchicalChunk.CreateChild(
            "Place the tiles on the board.",
            level: 2,
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "NarrativeText", DocumentId = _pdfDocumentId },
            parent.Id);
        var child2 = HierarchicalChunk.CreateChild(
            "Each player draws three cards.",
            level: 2,
            new ChunkMetadata { Page = 2, Heading = "Setup", ElementType = "NarrativeText", DocumentId = _pdfDocumentId },
            parent.Id);

        _advancedChunkingMock
            .Setup(s => s.ChunkDocumentAsync(
                It.IsAny<ExtractedDocument>(),
                It.IsAny<ChunkingConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { parent, child1, child2 });
    }

    private void SetupEmbeddings()
    {
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) =>
                new EmbeddingResult
                {
                    Success = true,
                    Embeddings = texts.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToList()
                });
        _embeddingServiceMock.Setup(e => e.GetModelName()).Returns("test-embedding-model");
    }

    /// <summary>
    /// Distinct, all-NON-None role per chunk so a working denormalization produces non-zero
    /// <c>RoleTags</c> on every pgvector row (and a broken one leaves them all 0).
    /// </summary>
    private void SetupRoleClassifierDistinctPerChunk()
    {
        var cycle = new[] { GameBookRole.Setup, GameBookRole.RulesReference, GameBookRole.Lore };
        _roleClassifierMock
            .Setup(c => c.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChunkInput> inputs, CancellationToken _) =>
                (IReadOnlyList<GameBookRole>)inputs.Select((_, i) => cycle[i % cycle.Length]).ToList());
    }

    /// <summary>
    /// The real <c>IPdfIndexingPipeline</c> creates the <c>vector_documents</c> row the pipeline
    /// re-reads before indexing. The fake reproduces that side effect so <c>IndexInVectorStoreAsync</c>
    /// reaches the pgvector batch build (otherwise it returns early on a missing VectorDocument).
    /// </summary>
    private void SetupIndexingPipelineCreatesVectorDocument()
    {
        _indexingPipelineMock
            .Setup(p => p.IndexAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid docId, Guid? gid, Guid? sgid, int chunkCount, int totalChars, string lang, CancellationToken ct) =>
            {
                _db.VectorDocuments.Add(new VectorDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    PdfDocumentId = docId,
                    GameId = gid,
                    SharedGameId = sgid,
                    ChunkCount = chunkCount,
                    TotalCharacters = totalChars,
                    IndexingStatus = "completed",
                    IndexedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
            });
    }
}
