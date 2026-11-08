using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
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
    private readonly Mock<IEmbeddingService> _embeddings = new();
    private readonly Mock<IBlobStorageService> _blobs = new();
    private readonly Mock<IAdvancedChunkingService> _advancedChunking = new();
    private readonly Guid _pdfId = Guid.NewGuid();
    private readonly Api.Tests.TestHelpers.RecordingLogger<PdfProcessingPipelineService> _log = new();
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

    /// <summary>What Docnet returns: the pages of the PDF's own text layer, marked as such (#4437).</summary>
    private static PagedTextExtractionResult TextLayerResult(params PageTextChunk[] pages) =>
        PagedTextExtractionResult.CreateSuccess(pages, pages.Length, pages.Sum(p => p.CharCount), ocrTriggered: false)
        with { TextLayerOnly = true };

    private void ExtractorReturns(PagedTextExtractionResult result) =>
        _extractor.Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void ChunkerReturnsNothing() =>
        _advancedChunking
            .Setup(s => s.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk>());

    private static bool Has(IReadOnlyDictionary<string, object?> state, string key, object? value) =>
        state.TryGetValue(key, out var found) && Equals(found, value);

    private static PageTextChunk EmptyPage(int number) => new(number, "", 0, 0);

    private static PageTextChunk TextPage(int number) => new(number, new string('a', 300), 0, 299);

    /// <summary>
    /// #4437: a scan. Docnet reads the text layer and does not OCR, so every page comes back empty. The row must say why,
    /// and must not be retried by <c>RetryFailedPdfsJob</c>: a category-less Failed is retried, and a scan does not grow
    /// text on the next attempt.
    /// </summary>
    [Fact]
    [Trait("Issue", "4437")]
    public async Task ProcessAsync_WhenNoPageHasText_FailsNamingTheCause_AndIsNotRetriable()
    {
        SeedPendingPdf();
        ExtractorReturns(TextLayerResult(EmptyPage(1), new PageTextChunk(2, "  ", 0, 0), EmptyPage(3)));
        ChunkerReturnsNothing();

        var outcome = await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        outcome.Should().Be(PdfPipelineOutcome.Failed);
        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.ProcessingState.Should().Be("Failed");
        pdf.ProcessingError.Should().Be(PdfFailureText.NoExtractableText);
        pdf.ErrorCategory.Should().Be(nameof(ErrorCategory.Parsing), "a scan does not grow text on a retry: RetryFailedPdfsJob must skip it");
        _log.Entries.Should().Contain(
            l => l.StartsWith("Warning", StringComparison.Ordinal) && l.Contains(_pdfId.ToString(), StringComparison.Ordinal)
                 && l.Contains("no extractable text", StringComparison.OrdinalIgnoreCase),
            "the log carries the detail the column leaves out");
    }

    /// <summary>
    /// #4437: the same empty pages from an extractor that is NOT text-layer-only (SmolDocling reads pixels, Unstructured
    /// may too, and a result that went through the orchestrator no longer says who produced it). «The extraction does not
    /// perform OCR» would be false for them, and an empty page is no proof of a scan: the failure stays what it was —
    /// the generic text and no category.
    /// </summary>
    [Fact]
    [Trait("Issue", "4437")]
    public async Task ProcessAsync_WhenNoPageHasText_ButTheExtractorIsNotTextLayerOnly_KeepsTheGenericFailure()
    {
        SeedPendingPdf();
        ExtractorReturns(TextLayerResult(EmptyPage(1), EmptyPage(2)) with { TextLayerOnly = false });
        ChunkerReturnsNothing();

        await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.ProcessingState.Should().Be("Failed");
        pdf.ProcessingError.Should().Be("Text extraction produced no usable chunks");
        pdf.ErrorCategory.Should().BeNull("unchanged: the generic failure keeps the behaviour it had");
        _log.Entries.Should().NotContain(l => l.Contains("no extractable text", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// #4437: the text IS there but the chunker produced nothing. That is not a scan, and the row must not say it is.
    /// </summary>
    [Fact]
    [Trait("Issue", "4437")]
    public async Task ProcessAsync_WhenThePagesHaveTextButNoChunkComes_DoesNotBlameAScan()
    {
        SeedPendingPdf();
        ChunkerReturnsNothing();

        await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.ProcessingError.Should().NotBe(PdfFailureText.NoExtractableText);
        pdf.ErrorCategory.Should().BeNull("unchanged: the generic failure keeps the behaviour it had");
    }

    /// <summary>
    /// #4437: some pages have text and some are scanned. The document reaches Ready with holes, and the only trace is a
    /// structured warning with the count — asserted on the fields, not on the formatted text, so a field renamed or
    /// dropped turns the test red.
    /// </summary>
    [Fact]
    [Trait("Issue", "4437")]
    public async Task ProcessAsync_WhenSomePagesAreNearlyEmpty_WritesAStructuredWarningWithTheCounts()
    {
        SeedPendingPdf();
        ExtractorReturns(TextLayerResult(TextPage(1), EmptyPage(2), TextPage(3)));
        SetupTwoChunks();
        _embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = false, ErrorMessage = "stop here: the warning is written at extraction" });

        await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        _log.States.Should().Contain(s =>
            Has(s, "PdfId", _pdfId)
            && Has(s, "NearlyEmptyPages", 1)
            && Has(s, "TotalPages", 3)
            && Has(s, "MaxCharacters", NearlyEmptyPages.MaxCharacters));
        _log.Entries.Should().Contain(
            l => l.StartsWith("Warning", StringComparison.Ordinal) && l.Contains("1 of 3 pages", StringComparison.Ordinal));
    }

    /// <summary>
    /// #4437: SmolDocling returns one <c>PageTextChunk</c> per chunk of its service, with page numbers that look real, so
    /// short chunks would be counted as nearly empty pages. Without the text-layer mark there is no warning.
    /// </summary>
    [Fact]
    [Trait("Issue", "4437")]
    public async Task ProcessAsync_WhenSomePagesAreShort_ButTheExtractorIsNotTextLayerOnly_WritesNoWarning()
    {
        SeedPendingPdf();
        ExtractorReturns(TextLayerResult(TextPage(1), new PageTextChunk(2, "a caption", 0, 8), TextPage(3)) with { TextLayerOnly = false });
        SetupTwoChunks();
        _embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = false, ErrorMessage = "stop here" });

        await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        _log.States.Should().NotContain(s => s.ContainsKey("NearlyEmptyPages"));
    }

    [Fact]
    [Trait("Issue", "4437")]
    public async Task ProcessAsync_WhenEveryPageHasText_WritesNoNearlyEmptyWarning()
    {
        SeedPendingPdf();
        ExtractorReturns(TextLayerResult(TextPage(1), TextPage(2)));
        SetupTwoChunks();
        _embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = false, ErrorMessage = "stop here" });

        await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        _log.States.Should().NotContain(s => s.ContainsKey("NearlyEmptyPages"));
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
        pdf.ProcessingError.Should().Be(
            PdfFailureText.For(ErrorCategory.Service),
            "#4660: the text is fixed by the category, the service's own message stays in the log");
        pdf.ProcessingError.Should().NotContain("embedding service down");
        _log.Entries.Should().Contain(l => l.Contains("embedding service down", StringComparison.Ordinal), "the detail is in the log");
        pdf.FailedAtState.Should().Be("Embedding");
        pdf.ErrorCategory.Should().Be(nameof(ErrorCategory.Service),
            "#4443: transitorio, come lo registra il task in background, così RetryFailedPdfsJob lo ritenta");
    }

    [Fact]
    [Trait("Issue", "4443")]
    public async Task ProcessAsync_WhenTheEmbeddingServiceThrows_EndsFailedAsService()
    {
        SeedPendingPdf();
        SetupTwoChunks();
        _embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var outcome = await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        outcome.Should().Be(PdfPipelineOutcome.Failed);
        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.FailedAtState.Should().Be("Embedding");
        pdf.ErrorCategory.Should().Be(nameof(ErrorCategory.Service));
        pdf.ProcessingError.Should().Be(PdfFailureText.For(ErrorCategory.Service));
        pdf.ProcessingError.Should().NotContain("Connection refused");
        _log.Entries.Should().Contain(l => l.Contains("Connection refused", StringComparison.Ordinal), "the detail is in the log");
    }

    /// <summary>
    /// #4660: l'eccezione imprevista (il ramo generico) porta un testo fisso, non il suo messaggio: quella colonna
    /// la serve pdf-status agli utenti. Il messaggio resta nel log, che il ramo scrive con l'eccezione.
    /// </summary>
    [Fact]
    [Trait("Issue", "4660")]
    public async Task ProcessAsync_WhenSomethingUnexpectedThrows_StoresTheFixedTextNotTheMessage()
    {
        SeedPendingPdf();
        _advancedChunking
            .Setup(s => s.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCastException("secret-host.internal:5432 /var/data/pdfs/rules.pdf"));

        var outcome = await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        outcome.Should().Be(PdfPipelineOutcome.Failed);
        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.ProcessingState.Should().Be("Failed");
        pdf.ProcessingError.Should().Be(PdfFailureText.For(ErrorCategory.Unknown));
        pdf.ProcessingError.Should().NotContain("secret-host");
        _log.Entries.Should().Contain(l => l.Contains("secret-host.internal", StringComparison.Ordinal), "the detail is in the log");
    }

    /// <summary>
    /// #4660: l'estrattore fallisce con un messaggio che nomina un servizio interno. La colonna porta il testo fisso
    /// della categoria: <c>PayloadTooLarge</c> se il fallimento è permanente, altrimenti nessuna categoria (ritentabile)
    /// e il testo di <c>Unknown</c>.
    /// </summary>
    [Theory]
    [Trait("Issue", "4660")]
    [InlineData(true, nameof(ErrorCategory.PayloadTooLarge))]
    [InlineData(false, null)]
    public async Task ProcessAsync_WhenTheExtractorFails_StoresTheFixedTextOfTheCategory(bool permanent, string? expectedCategory)
    {
        SeedPendingPdf();
        _extractor.Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateFailure(
                "unstructured-service at http://10.0.0.5:8000 answered 413", isPermanentFailure: permanent));

        var outcome = await CreatePipeline().ProcessAsync(_pdfId, "pdfs/game/file_rules.pdf", Guid.NewGuid(), CancellationToken.None);

        outcome.Should().Be(PdfPipelineOutcome.Failed);
        var pdf = await _db.PdfDocuments.AsNoTracking().SingleAsync();
        pdf.ErrorCategory.Should().Be(expectedCategory);
        pdf.ProcessingError.Should().Be(PdfFailureText.For(permanent ? ErrorCategory.PayloadTooLarge : ErrorCategory.Unknown));
        pdf.ProcessingError.Should().NotContain("10.0.0.5");
        _log.Entries.Should().Contain(l => l.Contains("10.0.0.5", StringComparison.Ordinal), "the detail is in the log");
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
        pdf.ErrorCategory.Should().Be(nameof(ErrorCategory.Service));
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
            Mock.Of<ITextChunkingService>(),
            _embeddings.Object,
            _blobs.Object,
            TimeProvider.System,
            _log,
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
