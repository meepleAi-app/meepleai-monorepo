using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #3589: staging burned 3 automatic retries on PDFs the Unstructured service permanently
/// rejects with 413 (file too large). Root cause: this pipeline's extraction-failure path
/// (used by RetryFailedPdfsJob and manual retry, via <see cref="ProcessAsync"/>) marked the
/// PdfDocument Failed via direct EF mutation without ever setting ErrorCategory — leaving it
/// NULL, which RetryFailedPdfsJob treats as retriable (see #3584). These tests pin that a
/// permanent extraction failure now lands on ErrorCategory.PayloadTooLarge (excluded from
/// RetryFailedPdfsJob's RetriableCategories), while a transient extraction failure keeps the
/// pre-existing NULL/retriable behavior unchanged.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3589")]
public sealed class PdfProcessingPipelineServiceExtractTextTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IPdfClaimService> _claimService = new();
    private readonly Mock<IPdfTextExtractor> _textExtractor = new();

    public PdfProcessingPipelineServiceExtractTextTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PdfPipelineExtractText_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);

        _blob.Setup(b => b.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
    }

    public void Dispose() => _db.Dispose();

    private async Task<PdfDocumentEntity> SeedPendingPdfAsync()
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "big-rulebook.pdf",
            FilePath = "/tmp/big-rulebook.pdf",
            FileSizeBytes = 65_824_691,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Pending",
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();
        return pdf;
    }

    private PdfProcessingPipelineService CreateSut()
    {
        return new PdfProcessingPipelineService(
            db: _db,
            pdfClaimService: _claimService.Object,
            pdfTextExtractor: _textExtractor.Object,
            tableExtractor: Mock.Of<IPdfTableExtractor>(),
            chunkingService: Mock.Of<ITextChunkingService>(),
            embeddingService: Mock.Of<IEmbeddingService>(),
            blobStorageService: _blob.Object,
            timeProvider: TimeProvider.System,
            logger: NullLogger<PdfProcessingPipelineService>.Instance,
            languageDetector: Mock.Of<ILanguageDetector>(),
            chunkTranslationService: Mock.Of<IChunkTranslationService>(),
            indexingPipeline: Mock.Of<IPdfIndexingPipeline>(),
            raptorIndexer: null,
            entityExtractor: null,
            vectorStore: null,
            featureFlagService: null,
            roleClassifier: null,
            pdfCoverExtractor: null,
            eventCollector: Mock.Of<IDomainEventCollector>(),
            pdfCoverUploadPipeline: null);
    }

    [Fact]
    public async Task ProcessAsync_PermanentExtractionFailure_MarksFailedWithPayloadTooLargeCategory()
    {
        var pdf = await SeedPendingPdfAsync();
        _claimService.Setup(c => c.TryClaimPendingAsync(pdf.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _textExtractor
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateFailure(
                "Unstructured extraction failed with status RequestEntityTooLarge: {\"detail\":{\"error\":{\"code\":\"FILE_TOO_LARGE\"}}}",
                isPermanentFailure: true));

        var sut = CreateSut();

        await sut.ProcessAsync(pdf.Id, pdf.FilePath, pdf.UploadedByUserId, CancellationToken.None);

        var reloaded = await _db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == pdf.Id);
        reloaded.ProcessingState.Should().Be("Failed");
        reloaded.ErrorCategory.Should().Be(nameof(ErrorCategory.PayloadTooLarge));
    }

    /// <summary>
    /// Regression pin: a transient extraction failure (e.g. Unstructured 500/timeout) must
    /// keep the pre-#3589 behavior — ErrorCategory left NULL, which RetryFailedPdfsJob treats
    /// as retriable (#3584). This fix targets ONLY the permanent case; it must not make
    /// ordinary transient failures newly non-retryable.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_TransientExtractionFailure_LeavesErrorCategoryNull()
    {
        var pdf = await SeedPendingPdfAsync();
        _claimService.Setup(c => c.TryClaimPendingAsync(pdf.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _textExtractor
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateFailure(
                "Unstructured extraction failed with status InternalServerError: {}",
                isPermanentFailure: false));

        var sut = CreateSut();

        await sut.ProcessAsync(pdf.Id, pdf.FilePath, pdf.UploadedByUserId, CancellationToken.None);

        var reloaded = await _db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == pdf.Id);
        reloaded.ProcessingState.Should().Be("Failed");
        reloaded.ErrorCategory.Should().BeNull();
    }
}
