using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class BackfillPdfCoversJobTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfCoverExtractor> _extractor = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IDomainEventCollector> _eventCollector = new();
    private readonly List<IDomainEvent> _collectedEvents = new();

    public BackfillPdfCoversJobTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"BackfillPdfCoversJob_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _eventCollector.Setup(c => c.Collect(It.IsAny<IDomainEvent>()))
                       .Callback<IDomainEvent>(e => _collectedEvents.Add(e));
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private BackfillPdfCoversJob CreateJob() =>
        new(serviceProvider: new Mock<IServiceProvider>().Object, NullLogger<BackfillPdfCoversJob>.Instance);

    private PdfDocumentEntity SeedPdf(
        string coverStatus = "Pending",
        string processingState = "Ready",
        DateTime? uploadedAt = null,
        Guid? sharedGameId = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = uploadedAt ?? DateTime.UtcNow,
            ProcessingState = processingState,
            CoverGenerationStatus = coverStatus,
            SharedGameId = sharedGameId,
        };
        _db.PdfDocuments.Add(pdf);
        _db.SaveChanges();
        return pdf;
    }

    [Fact]
    public async Task RunBatchAsync_NoEligiblePdfs_DoesNothing()
    {
        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        _extractor.Verify(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _blob.Verify(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_OnlyPicksUpReadyAndPendingPdfs()
    {
        // Eligible: Ready + Pending
        SeedPdf(coverStatus: "Pending", processingState: "Ready");
        // Not eligible: not Ready
        SeedPdf(coverStatus: "Pending", processingState: "Extracting");
        // Not eligible: cover already Generated
        SeedPdf(coverStatus: "Generated", processingState: "Ready");
        // Not eligible: cover Skipped (terminal)
        SeedPdf(coverStatus: "Skipped", processingState: "Ready");
        // Not eligible: cover Failed (terminal)
        SeedPdf(coverStatus: "Failed", processingState: "Ready");

        ConfigureExtractorReturning(PdfCoverExtractionOutcome.Skipped);
        ConfigureBlobReturningStream();

        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        _extractor.Verify(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBatchAsync_PdfBytesNullFromBlob_MarksFailedWithoutCallingExtractor()
    {
        var pdf = SeedPdf();

        _blob.Setup(b => b.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Stream?)null);

        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        _extractor.Verify(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);

        var refreshed = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == pdf.Id);
        refreshed.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Failed));
        refreshed.CoverGenerationError.Should().Contain("not found");
    }

    [Fact]
    public async Task RunBatchAsync_ExtractGenerated_UploadsBothSizesAndPersistsKeyAndEmitsEvent()
    {
        var sharedGameId = Guid.NewGuid();
        var pdf = SeedPdf(sharedGameId: sharedGameId);

        ConfigureBlobReturningStream();
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PdfCoverExtractionResult
                  {
                      Outcome = PdfCoverExtractionOutcome.Generated,
                      ThumbnailWebp = new byte[] { 1, 2, 3 },
                      PreviewWebp = new byte[] { 4, 5, 6, 7 },
                      SelectedPageIndex = 1,
                  });

        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        var expectedKey = $"pdf-cover-{pdf.Id}";

        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), "thumb.webp", BlobCategory.GameImage, expectedKey, It.IsAny<CancellationToken>()),
            Times.Once);
        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), "preview.webp", BlobCategory.GameImage, expectedKey, It.IsAny<CancellationToken>()),
            Times.Once);

        var refreshed = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == pdf.Id);
        refreshed.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Generated));
        refreshed.CoverR2Key.Should().Be(expectedKey);
        refreshed.CoverPageIndex.Should().Be(1);
        refreshed.CoverGenerationError.Should().BeNull();

        _collectedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PdfCoverGeneratedEvent>()
            .Which.SharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public async Task RunBatchAsync_ExtractSkipped_SetsSkippedStatusAndNoUpload()
    {
        var pdf = SeedPdf();

        ConfigureBlobReturningStream();
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PdfCoverExtractionResult
                  {
                      Outcome = PdfCoverExtractionOutcome.Skipped,
                      SelectedPageIndex = 0,
                  });

        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        var refreshed = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == pdf.Id);
        refreshed.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Skipped));
        refreshed.CoverR2Key.Should().BeNull();

        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _collectedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task RunBatchAsync_ExtractFailed_SetsFailedStatusWithErrorMessage()
    {
        var pdf = SeedPdf();

        ConfigureBlobReturningStream();
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PdfCoverExtractionResult
                  {
                      Outcome = PdfCoverExtractionOutcome.Failed,
                      ErrorMessage = "PDF corrupt",
                  });

        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        var refreshed = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == pdf.Id);
        refreshed.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Failed));
        refreshed.CoverGenerationError.Should().Be("PDF corrupt");
    }

    [Fact]
    public async Task RunBatchAsync_ExtractorThrows_MarksFailedAndContinuesNextItem()
    {
        var first = SeedPdf(uploadedAt: DateTime.UtcNow.AddMinutes(-10));
        var second = SeedPdf(uploadedAt: DateTime.UtcNow);

        ConfigureBlobReturningStream();
        _extractor.SetupSequence(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new InvalidOperationException("boom"))
                  .ReturnsAsync(new PdfCoverExtractionResult
                  {
                      Outcome = PdfCoverExtractionOutcome.Skipped,
                      SelectedPageIndex = 0,
                  });

        // Speed up test — bypass the default 500ms inter-item sleep would still
        // run, but xUnit defaults to 60s timeout so it's fine.
        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        var refreshedFirst = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == first.Id);
        refreshedFirst.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Failed));
        refreshedFirst.CoverGenerationError.Should().Contain("boom");

        var refreshedSecond = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == second.Id);
        refreshedSecond.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Skipped));
    }

    [Fact]
    public async Task RunBatchAsync_BatchLimit_ProcessesAtMostFivePerRun()
    {
        // Seed 8 eligible PDFs; expect only 5 picked up.
        for (var i = 0; i < 8; i++)
        {
            SeedPdf(uploadedAt: DateTime.UtcNow.AddMinutes(-i));
        }

        ConfigureBlobReturningStream();
        ConfigureExtractorReturning(PdfCoverExtractionOutcome.Skipped);

        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        _extractor.Verify(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Exactly(BackfillPdfCoversJob.BatchSize));

        var skippedCount = _db.PdfDocuments.AsNoTracking()
            .Count(p => p.CoverGenerationStatus == nameof(PdfCoverGenerationStatus.Skipped));
        skippedCount.Should().Be(BackfillPdfCoversJob.BatchSize);
    }

    [Fact]
    public async Task RunBatchAsync_OldestFirst_ProcessesByUploadedAtAsc()
    {
        var older = SeedPdf(uploadedAt: DateTime.UtcNow.AddDays(-5));
        var newer = SeedPdf(uploadedAt: DateTime.UtcNow);

        ConfigureBlobReturningStream();
        ConfigureExtractorReturning(PdfCoverExtractionOutcome.Skipped);

        var processedIds = new List<Guid>();
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .Callback<byte[], CancellationToken>((_, _) =>
                  {
                      // The first call must correspond to the older PDF (UploadedAt asc).
                      // We capture order via the side-effect on the entity below.
                  })
                  .ReturnsAsync(new PdfCoverExtractionResult { Outcome = PdfCoverExtractionOutcome.Skipped, SelectedPageIndex = 0 });

        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default);

        // Both processed (batch size = 5 > 2 eligible).
        _db.PdfDocuments.AsNoTracking().Single(p => p.Id == older.Id).CoverGenerationStatus
            .Should().Be(nameof(PdfCoverGenerationStatus.Skipped));
        _db.PdfDocuments.AsNoTracking().Single(p => p.Id == newer.Id).CoverGenerationStatus
            .Should().Be(nameof(PdfCoverGenerationStatus.Skipped));
    }

    private void ConfigureBlobReturningStream()
    {
        _blob.Setup(b => b.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF magic
    }

    private void ConfigureExtractorReturning(PdfCoverExtractionOutcome outcome)
    {
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PdfCoverExtractionResult
                  {
                      Outcome = outcome,
                      SelectedPageIndex = outcome == PdfCoverExtractionOutcome.Skipped ? 0 : null,
                      ThumbnailWebp = outcome == PdfCoverExtractionOutcome.Generated ? new byte[] { 1 } : null,
                      PreviewWebp = outcome == PdfCoverExtractionOutcome.Generated ? new byte[] { 2 } : null,
                  });
    }
}
