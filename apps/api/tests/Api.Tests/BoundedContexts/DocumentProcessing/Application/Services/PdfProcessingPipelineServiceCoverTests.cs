using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfProcessingPipelineServiceCoverTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfCoverExtractor> _coverExtractor = new();
    private readonly Mock<IPdfCoverUploadPipeline> _coverPipeline = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IDomainEventCollector> _eventCollector = new();
    private readonly List<IDomainEvent> _collected = new();

    public PdfProcessingPipelineServiceCoverTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PdfPipelineCover_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _eventCollector.Setup(c => c.Collect(It.IsAny<IDomainEvent>()))
                       .Callback<IDomainEvent>(e => _collected.Add(e));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ExtractCoverImageAsync_Generated_UploadsPreviewViaPipelineWithDeterministicKey()
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Extracting",
            CoverGenerationStatus = "Pending",
            SharedGameId = Guid.NewGuid(),
        };

        _blob.Setup(b => b.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
        _coverExtractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new PdfCoverExtractionResult
                       {
                           Outcome = PdfCoverExtractionOutcome.Generated,
                           ThumbnailWebp = new byte[] { 1 },
                           PreviewWebp = new byte[] { 9, 9, 9 },
                           SelectedPageIndex = 0,
                       });

        var expectedKey = $"covers/pdf/{pdf.Id:D}/cover";
        _coverPipeline.Setup(p => p.UploadAsync(expectedKey, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedKey);

        var sut = PdfProcessingPipelineServiceCoverTestFactory.Create(
            _db, _blob.Object, _coverExtractor.Object, _coverPipeline.Object, _eventCollector.Object);

        await sut.InvokeExtractCoverImageForTestAsync(pdf, "/tmp/rules.pdf", CancellationToken.None);

        _coverPipeline.Verify(p => p.UploadAsync(
            expectedKey,
            It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 9, 9, 9 })),
            It.IsAny<CancellationToken>()), Times.Once);
        _blob.Verify(b => b.StoreAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.GameImage, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        pdf.CoverR2Key.Should().Be(expectedKey);
        pdf.CoverGenerationStatus.Should().Be("Generated");

        _collected.Should().ContainSingle()
            .Which.Should().BeOfType<PdfCoverGeneratedEvent>()
            .Which.CoverR2Key.Should().Be(expectedKey);
    }

    /// <summary>
    /// Issue #3363: in local-storage mode <see cref="IPdfCoverUploadPipeline"/> is unregistered → the
    /// optional ctor param resolves to null while <see cref="IPdfCoverExtractor"/> stays registered.
    /// This is the exact production shape the fix protects: the cover step must early-return BEFORE
    /// touching the extractor (so no wasted render + no NPE on the null pipeline) and leave the PDF's
    /// cover state untouched, so PDF ingestion completes instead of failing at Upload.
    /// </summary>
    [Fact]
    public async Task ExtractCoverImageAsync_LocalStorageNullUploadPipeline_SkipsWithoutExtracting()
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Extracting",
            CoverGenerationStatus = "Pending",
            SharedGameId = Guid.NewGuid(),
        };

        // Non-null extractor, NULL upload pipeline — the local-storage production combination.
        var sut = PdfProcessingPipelineServiceCoverTestFactory.Create(
            _db, _blob.Object, _coverExtractor.Object, coverUploadPipeline: null, _eventCollector.Object);

        var act = () => sut.InvokeExtractCoverImageForTestAsync(pdf, "/tmp/rules.pdf", CancellationToken.None);

        await act.Should().NotThrowAsync();
        _coverExtractor.Verify(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never,
            "with the upload pipeline null the cover step must short-circuit before rendering");
        pdf.CoverGenerationStatus.Should().Be("Pending", "cover state is left untouched when skipped");
        pdf.CoverR2Key.Should().BeNull();
        _collected.Should().BeEmpty();
    }

    // #3373 D1: an exception thrown during extract/upload/save is an INFRA failure (R2/DB) — TRANSIENT.
    // The cover step must return the PDF to Pending (retry-eligible via BackfillPdfCoversJob) while the
    // attempt budget has room, then terminal Failed once it is exhausted. These two tests give this
    // call-site the same coverage BackfillPdfCoversJob already has for its transient paths.

    [Fact]
    public async Task ExtractCoverImageAsync_UploadThrows_TransientRetryPendingAndIncrementsAttempts()
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Extracting",
            CoverGenerationStatus = "Pending",
            CoverGenerationAttempts = 0,
            SharedGameId = Guid.NewGuid(),
        };

        _blob.Setup(b => b.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
        _coverExtractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new PdfCoverExtractionResult
                       {
                           Outcome = PdfCoverExtractionOutcome.Generated,
                           ThumbnailWebp = new byte[] { 1 },
                           PreviewWebp = new byte[] { 9, 9, 9 },
                           SelectedPageIndex = 0,
                       });
        // R2 upload throws — the realistic transient infra failure the catch protects against.
        _coverPipeline.Setup(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new InvalidOperationException("R2 upload transient failure"));

        var sut = PdfProcessingPipelineServiceCoverTestFactory.Create(
            _db, _blob.Object, _coverExtractor.Object, _coverPipeline.Object, _eventCollector.Object);

        var act = () => sut.InvokeExtractCoverImageForTestAsync(pdf, "/tmp/rules.pdf", CancellationToken.None);

        await act.Should().NotThrowAsync("a transient cover failure must not abort PDF ingestion");
        pdf.CoverGenerationStatus.Should().Be("Pending", "the failure is retry-eligible while budget remains");
        pdf.CoverGenerationAttempts.Should().Be(1, "the attempt counter advances on each transient failure");
        pdf.CoverR2Key.Should().BeNull();
        _collected.Should().BeEmpty("no propagation event on a failed generation");
    }

    [Fact]
    public async Task ExtractCoverImageAsync_UploadThrowsAtLastAttempt_TerminalFailed()
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Extracting",
            CoverGenerationStatus = "Pending",
            CoverGenerationAttempts = PdfCoverRetryPolicy.MaxAttempts - 1,
            SharedGameId = Guid.NewGuid(),
        };

        _blob.Setup(b => b.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
        _coverExtractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new PdfCoverExtractionResult
                       {
                           Outcome = PdfCoverExtractionOutcome.Generated,
                           ThumbnailWebp = new byte[] { 1 },
                           PreviewWebp = new byte[] { 9, 9, 9 },
                           SelectedPageIndex = 0,
                       });
        _coverPipeline.Setup(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new InvalidOperationException("R2 upload transient failure"));

        var sut = PdfProcessingPipelineServiceCoverTestFactory.Create(
            _db, _blob.Object, _coverExtractor.Object, _coverPipeline.Object, _eventCollector.Object);

        await sut.InvokeExtractCoverImageForTestAsync(pdf, "/tmp/rules.pdf", CancellationToken.None);

        pdf.CoverGenerationStatus.Should().Be("Failed", "the retry budget is exhausted — the failure is now terminal");
        pdf.CoverGenerationAttempts.Should().Be(PdfCoverRetryPolicy.MaxAttempts);
    }
}
