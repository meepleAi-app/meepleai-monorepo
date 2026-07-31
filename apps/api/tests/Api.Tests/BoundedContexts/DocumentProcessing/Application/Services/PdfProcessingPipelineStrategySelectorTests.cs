using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
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
/// DC-2 (#3419): <see cref="PdfProcessingPipelineService"/> decides the Unstructured extraction
/// strategy from the PDF's <b>prior</b> <c>StructuredElementsJson</c> (a <c>Table</c> ⇒ HiRes, else
/// Fast) and publishes it on the scoped <see cref="IExtractionStrategySelector"/> BEFORE invoking the
/// extractor — so the same-scope <c>UnstructuredPdfTextExtractor</c> reads it. The value is captured
/// at the extractor call because the pipeline overwrites <c>StructuredElementsJson</c> with the fresh
/// extraction result immediately afterwards.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3419")]
public sealed class PdfProcessingPipelineStrategySelectorTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IChunkTranslationService> _chunkTranslationServiceMock = new();
    private readonly Mock<ILanguageDetector> _languageDetectorMock = new();
    private readonly ExtractionStrategySelector _selector = new();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public PdfProcessingPipelineStrategySelectorTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"PdfPipelineStrategyTest_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _languageDetectorMock
            .Setup(l => l.Detect(It.IsAny<string>()))
            .Returns(new LanguageDetectionResult("en", true, 0.95));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessAsync_PriorStructuredElementsContainTable_SelectsHiRes()
    {
        SeedPdfDocument(priorStructuredElementsJson: ElementsJson(("Title", 1), ("NarrativeText", 1), ("Table", 2)));
        SetupBlobStorage();
        var capturedStrategy = SetupExtractorCapturingStrategy();

        await CreateSut().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        capturedStrategy().Should().Be(ExtractionStrategy.HiRes);
    }

    [Fact]
    public async Task ProcessAsync_PriorStructuredElementsHaveNoTable_SelectsFast()
    {
        SeedPdfDocument(priorStructuredElementsJson: ElementsJson(("Title", 1), ("NarrativeText", 1), ("ListItem", 2)));
        SetupBlobStorage();
        var capturedStrategy = SetupExtractorCapturingStrategy();

        await CreateSut().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        capturedStrategy().Should().Be(ExtractionStrategy.Fast);
    }

    [Fact]
    public async Task ProcessAsync_NoPriorStructuredElements_SelectsFast()
    {
        // Fresh ingest: no prior elements → cannot know about tables → Fast.
        SeedPdfDocument(priorStructuredElementsJson: null);
        SetupBlobStorage();
        var capturedStrategy = SetupExtractorCapturingStrategy();

        await CreateSut().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        capturedStrategy().Should().Be(ExtractionStrategy.Fast);
    }

    // Mirror the pipeline's serialization: default JsonSerializer over List<ExtractedElement>.
    private static string ElementsJson(params (string type, int page)[] els) =>
        JsonSerializer.Serialize(
            els.Select(e => new ExtractedElement($"{e.type} text", e.page, e.type)).ToList());

    private PdfProcessingPipelineService CreateSut() =>
        new(
            _db,
            new Api.Tests.TestHelpers.InMemoryPdfClaimService(_db),
            _pdfTextExtractorMock.Object,
            Mock.Of<IPdfTableExtractor>(),
            Mock.Of<ITextChunkingService>(),
            Mock.Of<IEmbeddingService>(),
            _blobStorageServiceMock.Object,
            TimeProvider.System,
            NullLogger<PdfProcessingPipelineService>.Instance,
            _languageDetectorMock.Object,
            _chunkTranslationServiceMock.Object,
            Mock.Of<IPdfIndexingPipeline>(),
            extractionStrategySelector: _selector);

    private void SeedPdfDocument(string? priorStructuredElementsJson)
    {
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
            StructuredElementsJson = priorStructuredElementsJson,
            UploadedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    private void SetupBlobStorage()
    {
        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF header
    }

    /// <summary>
    /// Captures <see cref="IExtractionStrategySelector.Current"/> at the exact moment the pipeline
    /// invokes the extractor, then returns a failure so the pipeline short-circuits after the decision
    /// (downstream chunking/embedding is irrelevant to this test — covered by the heading-aware suite).
    /// </summary>
    private Func<ExtractionStrategy?> SetupExtractorCapturingStrategy()
    {
        ExtractionStrategy? captured = null;
        _pdfTextExtractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => captured = _selector.Current)
            .ReturnsAsync(PagedTextExtractionResult.CreateFailure("stop after strategy decision"));
        return () => captured;
    }
}
