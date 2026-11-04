using Api.BoundedContexts.DocumentProcessing.Application.Services;
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
/// #4435: <see cref="PdfProcessingPipelineService"/> publishes what it knows about the document's
/// language on the scoped <see cref="IExtractionLanguageSelector"/> BEFORE invoking the extractor, so
/// the same-scope <c>UnstructuredPdfTextExtractor</c> can send it. Language detection runs AFTER the
/// extraction (it reads the extracted text): on a first ingest the row still carries the entity
/// default <c>Language = "en"</c> with <c>LanguageConfidence = null</c>, and the resolver treats that as
/// "not known". The hint is cleared after the call so it cannot leak to a later extraction in the scope.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4435")]
public sealed class PdfProcessingPipelineLanguageSelectorTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<ILanguageDetector> _languageDetectorMock = new();
    private readonly ExtractionLanguageSelector _selector = new();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();

    public PdfProcessingPipelineLanguageSelectorTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"PdfPipelineLanguageTest_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _languageDetectorMock
            .Setup(l => l.Detect(It.IsAny<string>()))
            .Returns(new LanguageDetectionResult("en", true, 0.95));

        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessAsync_FirstIngest_PublishesUndetectedLanguage()
    {
        SeedPdfDocument(language: "en", confidence: null, languageOverride: null);
        var captured = SetupExtractorCapturingHint();

        await CreateSut().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        captured().Should().Be(new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "en", DetectedConfidence: null));
    }

    [Fact]
    public async Task ProcessAsync_Reprocess_PublishesPriorDetectionAndOverride()
    {
        SeedPdfDocument(language: "it", confidence: 0.97, languageOverride: "de");
        var captured = SetupExtractorCapturingHint();

        await CreateSut().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        captured().Should().Be(new DocumentLanguageHint(LanguageOverride: "de", DetectedLanguage: "it", DetectedConfidence: 0.97));
    }

    [Fact]
    public async Task ProcessAsync_ClearsTheHintAfterExtraction()
    {
        SeedPdfDocument(language: "it", confidence: 0.97, languageOverride: null);
        SetupExtractorCapturingHint();

        await CreateSut().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        _selector.Current.Should().BeNull();
    }

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
            Mock.Of<IChunkTranslationService>(),
            Mock.Of<IPdfIndexingPipeline>(),
            extractionLanguageSelector: _selector);

    private void SeedPdfDocument(string language, double? confidence, string? languageOverride)
    {
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            PrivateGameId = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/fake/path/test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Pending",
            Language = language,
            LanguageConfidence = confidence,
            LanguageOverride = languageOverride,
            UploadedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    /// <summary>
    /// Captures <see cref="IExtractionLanguageSelector.Current"/> at the moment the pipeline invokes the
    /// extractor, then fails the extraction so the pipeline stops right after (chunking is irrelevant).
    /// </summary>
    private Func<DocumentLanguageHint?> SetupExtractorCapturingHint()
    {
        DocumentLanguageHint? captured = null;
        _pdfTextExtractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => captured = _selector.Current)
            .ReturnsAsync(PagedTextExtractionResult.CreateFailure("stop after language decision"));
        return () => captured;
    }
}
