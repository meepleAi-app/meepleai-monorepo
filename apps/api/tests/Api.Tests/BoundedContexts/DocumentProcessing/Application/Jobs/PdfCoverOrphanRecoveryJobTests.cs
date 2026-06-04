using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfCoverOrphanRecoveryJobTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IBlobStorageService> _blob = new();

    public PdfCoverOrphanRecoveryJobTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PdfCoverOrphanRecoveryJob_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private PdfCoverOrphanRecoveryJob CreateJob() =>
        new(new Mock<IServiceProvider>().Object, NullLogger<PdfCoverOrphanRecoveryJob>.Instance);

    private PdfDocumentEntity SeedPdf(
        string? coverR2Key = null,
        string coverStatus = "Generated",
        DateTime? updatedAt = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            CoverGenerationStatus = coverStatus,
            CoverR2Key = coverR2Key,
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
        };
        _db.PdfDocuments.Add(pdf);
        _db.SaveChanges();
        return pdf;
    }

    [Fact]
    public async Task RunBatchAsync_NoGeneratedPdfs_NoOp()
    {
        // No PDFs in DB at all → ExistsAsync must never be called
        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        _blob.Verify(b => b.ExistsAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_AllGeneratedExist_NoReset()
    {
        // 3 PDFs, all with CoverR2Key, all returning true from ExistsAsync
        SeedPdf(coverR2Key: "pdf-cover-aaa");
        SeedPdf(coverR2Key: "pdf-cover-bbb");
        SeedPdf(coverR2Key: "pdf-cover-ccc");

        _blob.Setup(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        // All statuses must remain Generated, keys must remain set
        var all = _db.PdfDocuments.AsNoTracking().ToList();
        all.Should().AllSatisfy(p =>
        {
            p.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Generated));
            p.CoverR2Key.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task RunBatchAsync_OrphanDetected_ResetsToPending()
    {
        // 1 PDF with key, ExistsAsync returns false → orphan → reset all 4 fields
        var orphan = SeedPdf(coverR2Key: "pdf-cover-missing");
        orphan.CoverGenerationError = "stale-error";
        orphan.CoverPageIndex = 2;
        _db.SaveChanges();

        _blob.Setup(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);

        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        var reset = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == orphan.Id);
        reset.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Pending));
        reset.CoverR2Key.Should().BeNull();
        reset.CoverGenerationError.Should().BeNull();
        reset.CoverPageIndex.Should().BeNull();
    }

    [Fact]
    public async Task RunBatchAsync_BatchSizeLimit_ProcessesMaxBatchSize()
    {
        // 51 Generated PDFs — only BatchSize (50) should be checked
        for (var i = 0; i < 51; i++)
        {
            SeedPdf(coverR2Key: $"pdf-cover-{i:D2}", updatedAt: DateTime.UtcNow.AddMinutes(-i));
        }

        _blob.Setup(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true); // All exist → no resets, only call count matters

        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        _blob.Verify(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(PdfCoverOrphanRecoveryJob.BatchSize));
    }

    [Fact]
    public async Task RunBatchAsync_ExistsAsyncThrows_LogsAndContinuesBatch()
    {
        // First PDF: ExistsAsync throws → skip (stays Generated)
        // Second PDF: ExistsAsync returns false → reset to Pending
        var first = SeedPdf(coverR2Key: "pdf-cover-throws", updatedAt: DateTime.UtcNow.AddMinutes(-10));
        var second = SeedPdf(coverR2Key: "pdf-cover-missing", updatedAt: DateTime.UtcNow);

        _blob.SetupSequence(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("Network error"))
             .ReturnsAsync(false);

        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        var firstResult = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == first.Id);
        firstResult.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Generated),
            "exception on first item must not corrupt its status — skip and continue");

        var secondResult = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == second.Id);
        secondResult.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Pending),
            "second orphan must be reset despite first item throwing");
        secondResult.CoverR2Key.Should().BeNull();
    }
}
