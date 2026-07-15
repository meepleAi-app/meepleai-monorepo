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

    // #2947 fix: the job checks existence via the RAW-KEY primitive
    // (GetPresignedUrlForRawKeyAsync), mirroring CoverUrlResolver, NOT the
    // categorized ExistsAsync (which runs PathSecurity.ValidateIdentifier and
    // rejects the '/' in the deterministic "covers/pdf/{id:D}/cover" key
    // convention). All mocks below target GetPresignedUrlForRawKeyAsync.

    [Fact]
    public async Task RunBatchAsync_NoGeneratedPdfs_NoOp()
    {
        // No PDFs in DB at all → the raw-key existence check must never be called
        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        _blob.Verify(b => b.GetPresignedUrlForRawKeyAsync(
                It.IsAny<string>(), It.IsAny<int?>()),
            Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_AllGeneratedExist_NoReset()
    {
        // 3 PDFs, all with CoverR2Key, all resolving to a non-null presigned URL
        SeedPdf(coverR2Key: "pdf-cover-aaa");
        SeedPdf(coverR2Key: "pdf-cover-bbb");
        SeedPdf(coverR2Key: "pdf-cover-ccc");

        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
             .ReturnsAsync("https://presigned.example/cover-preview.webp");

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
        // 1 PDF with key, raw-key check returns null (object absent) → orphan → reset all 4 fields
        var orphan = SeedPdf(coverR2Key: "pdf-cover-missing");
        orphan.CoverGenerationError = "stale-error";
        orphan.CoverPageIndex = 2;
        _db.SaveChanges();

        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
             .ReturnsAsync((string?)null);

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

        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
             .ReturnsAsync("https://presigned.example/cover-preview.webp"); // All exist → no resets, only call count matters

        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        _blob.Verify(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()),
            Times.Exactly(PdfCoverOrphanRecoveryJob.BatchSize));
    }

    [Fact]
    public async Task RunBatchAsync_ExistsAsyncThrows_LogsAndContinuesBatch()
    {
        // First PDF: raw-key check throws → skip (stays Generated)
        // Second PDF: raw-key check returns null → reset to Pending
        var first = SeedPdf(coverR2Key: "pdf-cover-throws", updatedAt: DateTime.UtcNow.AddMinutes(-10));
        var second = SeedPdf(coverR2Key: "pdf-cover-missing", updatedAt: DateTime.UtcNow);

        _blob.SetupSequence(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
             .ThrowsAsync(new InvalidOperationException("Network error"))
             .ReturnsAsync((string?)null);

        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        var firstResult = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == first.Id);
        firstResult.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Generated),
            "exception on first item must not corrupt its status — skip and continue");

        var secondResult = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == second.Id);
        secondResult.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Pending),
            "second orphan must be reset despite first item throwing");
        secondResult.CoverR2Key.Should().BeNull();
    }

    [Fact]
    public async Task RunBatchAsync_NewConventionKey_RawKeyExists_NoReset()
    {
        var pdf = SeedPdf(coverR2Key: $"covers/pdf/{Guid.NewGuid():D}/cover");

        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync(
                $"{pdf.CoverR2Key}-preview.webp", It.IsAny<int?>()))
             .ReturnsAsync("https://presigned.example/cover-preview.webp");

        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        var result = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == pdf.Id);
        result.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Generated),
            "the raw object exists in R2 — a valid new-convention cover must not be reset");
        result.CoverR2Key.Should().Be(pdf.CoverR2Key);
    }

    [Fact]
    public async Task RunBatchAsync_NewConventionKey_RawKeyMissing_ResetsToPending()
    {
        var pdf = SeedPdf(coverR2Key: $"covers/pdf/{Guid.NewGuid():D}/cover");

        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync(
                $"{pdf.CoverR2Key}-preview.webp", It.IsAny<int?>()))
             .ReturnsAsync((string?)null);

        await CreateJob().RunBatchAsync(_db, _blob.Object, CancellationToken.None);

        var result = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == pdf.Id);
        result.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Pending));
        result.CoverR2Key.Should().BeNull();
    }
}
