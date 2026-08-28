using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
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
/// Issue #3846: with <c>STORAGE_PROVIDER=s3</c> an upload answers 200 and the processing fails
/// immediately after, with "Could not find a part of the path '/app/pdfs/...'".
///
/// The pipeline does read through <see cref="IBlobStorageService"/>, but it rebuilds the
/// resourceKey as <c>PdfStorageKey.ForPdf(pdfId)</c> while <c>UploadPdfCommandHandler</c> stored
/// the object under <c>gameId ?? privateGameId</c>. Both blob backends resolve by exact
/// <c>{category}/{resourceKey}/{fileId}_</c> prefix with no cross-folder search, so the read finds
/// nothing — and the filesystem fallback then opens the persisted <c>FilePath</c>, which under S3
/// is an object KEY, not a path. Same defect as #3568, fixed there only on the download path.
///
/// The blob double below <b>refuses reads outside the key it was written to</b>: that is the whole
/// point. The pre-existing suite mocks <c>RetrieveAsync</c> with <c>It.IsAny&lt;string&gt;()</c> on
/// every argument, so it answers any resourceKey and the defect stays invisible — exactly like
/// <c>STORAGE_PROVIDER=local</c>, where the fallback opens a real absolute path and covers the miss.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3846")]
public sealed class PdfPipelineBlobResourceKeyTests : IDisposable
{
    /// <summary>The resourceKey used at write time by <c>UploadPdfCommandHandler</c>: the gameId.</summary>
    private const string UploadResourceKey = "7f3a1c2e-0000-4444-8888-abcdefabcdef";

    private static readonly byte[] StoredPdfBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };

    private readonly MeepleAiDbContext _db;
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IPdfClaimService> _claimService = new();
    private readonly Mock<IPdfTextExtractor> _textExtractor = new();

    private readonly string _storedFileId = Guid.NewGuid().ToString("N");

    public PdfPipelineBlobResourceKeyTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PdfPipelineBlobResourceKey_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);

        // The object exists ONLY at the key it was written to. Any other (fileId, resourceKey)
        // pair returns null, like a real bucket would.
        _blob
            .Setup(b => b.RetrieveAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileId, BlobCategory category, string resourceKey, CancellationToken _) =>
                fileId == _storedFileId
                && category == BlobCategory.Pdf
                && string.Equals(resourceKey, UploadResourceKey, StringComparison.Ordinal)
                    ? new MemoryStream(StoredPdfBytes)
                    : null);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Seeds a PDF whose <c>FilePath</c> has the shape an S3 upload leaves behind:
    /// <c>{category}/{gameId}/{fileId}_{name}</c> — a bucket key, not a filesystem path,
    /// and with a resourceKey that is NOT the pdfId.
    /// </summary>
    private async Task<PdfDocumentEntity> SeedUploadedPdfAsync()
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "manuale.pdf",
            FilePath = $"pdfs/{UploadResourceKey}/{_storedFileId}_manuale.pdf",
            FileSizeBytes = StoredPdfBytes.Length,
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

    /// <summary>
    /// The bytes handed to the extractor must be the bytes that are actually in storage, at the key
    /// the upload wrote. Before the fix the read asks for <c>pdfs/{pdfId}/…</c>, gets nothing, and
    /// the filesystem fallback throws on a path that never existed — the extractor is never reached.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ReadsTheBlobFromTheKeyTheUploadWroteIt_NotFromTheRebuiltPdfIdKey()
    {
        var pdf = await SeedUploadedPdfAsync();
        _claimService.Setup(c => c.TryClaimPendingAsync(pdf.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        byte[]? bytesHandedToExtractor = null;
        _textExtractor
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream stream, bool _, CancellationToken _) =>
            {
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                bytesHandedToExtractor = buffer.ToArray();

                // Stop the pipeline right after the read: this test is about the read, and a
                // transient failure keeps the pre-existing retriable behaviour untouched.
                return PagedTextExtractionResult.CreateFailure("stop after read");
            });

        var sut = CreateSut();

        await sut.ProcessAsync(pdf.Id, pdf.FilePath, pdf.UploadedByUserId, CancellationToken.None);

        bytesHandedToExtractor.Should().Equal(
            StoredPdfBytes,
            "the pipeline must read the object at the key persisted in FilePath, not at a key rebuilt from the pdfId");
    }

    /// <summary>
    /// Issue #3846: when the object really is absent from the bucket, the failure must say so. The
    /// filesystem fallback used to run unconditionally and open the persisted FilePath, which under
    /// S3 is an object key: the recorded error then pointed at a path that never existed
    /// ("Could not find a part of the path '/app/pdfs/…'") and landed on a NULL/Unknown category that
    /// <c>RetryFailedPdfsJob</c> treats as retriable — burning retries on an object that is not there.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenTheObjectIsAbsentFromStorage_FailsAsStorageObjectMissingNamingTheKey()
    {
        var pdf = await SeedUploadedPdfAsync();
        _claimService.Setup(c => c.TryClaimPendingAsync(pdf.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Nothing was ever stored under this pdf's key, and FilePath is a bucket key, not a path.
        var missingFileId = Guid.NewGuid().ToString("N");
        pdf.FilePath = $"pdfs/{UploadResourceKey}/{missingFileId}_manuale.pdf";
        await _db.SaveChangesAsync();

        var sut = CreateSut();

        await sut.ProcessAsync(pdf.Id, pdf.FilePath, pdf.UploadedByUserId, CancellationToken.None);

        var reloaded = await _db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == pdf.Id);
        reloaded.ProcessingState.Should().Be("Failed");
        reloaded.ErrorCategory.Should().Be(
            nameof(ErrorCategory.StorageObjectMissing),
            "an object that is not in the bucket does not reappear on retry");
        reloaded.ProcessingError.Should().Contain(
            missingFileId,
            "the recorded error must name the key that was looked up, not a filesystem path derived from it");
        reloaded.ProcessingError.Should().Contain(UploadResourceKey);
    }
}
