using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Seeders.Catalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

/// <summary>
/// Unit tests for #2908 — re-enqueue PDFs stranded in Pending with no active ProcessingJob.
/// In-memory EF (FK not enforced) is sufficient: we assert selection + enqueue-shape + idempotency.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ReattemptStalePendingPdfsSeederTests
{
    private readonly Mock<ILogger> _logger = new();
    private readonly Guid _systemUserId = Guid.NewGuid();

    private static Guid SeedGame(MeepleAiDbContext db, bool softDeleted = false)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Test Game", IsDeleted = softDeleted });
        return gameId;
    }

    private static Guid SeedPdf(MeepleAiDbContext db, Guid? gameId, string state)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "rules.pdf",
            FilePath = $"pdfs/{pdfId}/file.pdf",
            ContentHash = "hash",
            UploadedByUserId = Guid.NewGuid(),
            DocumentType = "base",
            DocumentCategory = "Rulebook",
            ProcessingState = state,
        });
        return pdfId;
    }

    private static void SeedJob(MeepleAiDbContext db, Guid pdfId, Guid userId, string status)
    {
        db.ProcessingJobs.Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            UserId = userId,
            Status = status,
            Priority = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            MaxRetries = 3,
            RetryCount = 0,
        });
    }

    [Fact]
    public async Task ReattemptAsync_PendingValidGameNoActiveJob_EnqueuesQueuedJobWithFiveSteps()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var pdfId = SeedPdf(db, gameId, nameof(PdfProcessingState.Pending));
        await db.SaveChangesAsync();

        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);

        var jobs = db.ProcessingJobs.Include(j => j.Steps).Where(j => j.PdfDocumentId == pdfId).ToList();
        jobs.Should().HaveCount(1);
        var job = jobs[0];
        job.Status.Should().Be(nameof(JobStatus.Queued));
        job.UserId.Should().Be(_systemUserId);
        job.Priority.Should().Be(0);
        job.MaxRetries.Should().Be(3);
        job.Steps.Should().HaveCount(5);
        job.Steps.Select(s => s.StepName).Should().BeEquivalentTo(new[]
        {
            nameof(ProcessingStepType.Upload),
            nameof(ProcessingStepType.Extract),
            nameof(ProcessingStepType.Chunk),
            nameof(ProcessingStepType.Embed),
            nameof(ProcessingStepType.Index),
        });
        job.Steps.Should().OnlyContain(s => s.Status == "Pending");
    }

    [Fact]
    public async Task ReattemptAsync_PendingWithQueuedJob_DoesNothing()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var pdfId = SeedPdf(db, gameId, nameof(PdfProcessingState.Pending));
        SeedJob(db, pdfId, _systemUserId, nameof(JobStatus.Queued));
        await db.SaveChangesAsync();

        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);

        db.ProcessingJobs.Where(j => j.PdfDocumentId == pdfId).Should().HaveCount(1);
    }

    [Fact]
    public async Task ReattemptAsync_PendingWithProcessingJob_DoesNothing()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var pdfId = SeedPdf(db, gameId, nameof(PdfProcessingState.Pending));
        SeedJob(db, pdfId, _systemUserId, nameof(JobStatus.Processing));
        await db.SaveChangesAsync();

        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);

        db.ProcessingJobs.Where(j => j.PdfDocumentId == pdfId).Should().HaveCount(1);
    }

    [Fact]
    public async Task ReattemptAsync_PendingWithOnlyTerminalJob_ReEnqueues()
    {
        // A stale Completed job (produced 0 chunks) is NOT active → the PDF is stranded → re-enqueue.
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var pdfId = SeedPdf(db, gameId, nameof(PdfProcessingState.Pending));
        SeedJob(db, pdfId, _systemUserId, nameof(JobStatus.Completed));
        await db.SaveChangesAsync();

        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);

        var jobs = db.ProcessingJobs.Where(j => j.PdfDocumentId == pdfId).ToList();
        jobs.Should().HaveCount(2); // the old Completed + the new Queued
        jobs.Count(j => j.Status == nameof(JobStatus.Queued)).Should().Be(1);
    }

    [Fact]
    public async Task ReattemptAsync_PendingButOrphanGame_Skips()
    {
        // SharedGame soft-deleted → excluded by the !IsDeleted filter → PDF is an orphan, out of #2908 scope.
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var orphanGameId = SeedGame(db, softDeleted: true);
        var pdfId = SeedPdf(db, orphanGameId, nameof(PdfProcessingState.Pending));
        await db.SaveChangesAsync();

        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);

        db.ProcessingJobs.Where(j => j.PdfDocumentId == pdfId).Should().BeEmpty();
    }

    [Fact]
    public async Task ReattemptAsync_NonPendingState_Skips()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var pdfId = SeedPdf(db, gameId, nameof(PdfProcessingState.Ready));
        await db.SaveChangesAsync();

        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);

        db.ProcessingJobs.Where(j => j.PdfDocumentId == pdfId).Should().BeEmpty();
    }

    [Fact]
    public async Task ReattemptAsync_RunTwice_IsIdempotent()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var pdfId = SeedPdf(db, gameId, nameof(PdfProcessingState.Pending));
        await db.SaveChangesAsync();

        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);
        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);

        db.ProcessingJobs.Where(j => j.PdfDocumentId == pdfId).Should().HaveCount(1);
    }

    [Fact]
    [Trait("Issue", "3075")]
    public async Task ReattemptAsync_PendingDemoMockPlaceholder_Skips()
    {
        // #3075: a Badsworm demo mock (seed/ prefix, no blob) seeded Pending for a valid game must
        // NOT be re-enqueued — processing it would fail on the missing blob and flip its intended
        // demo state to Failed.
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = SeedGame(db);
        var mockId = SeedMockPdf(db, gameId);
        await db.SaveChangesAsync();

        await ReattemptStalePendingPdfsSeeder.ReattemptAsync(db, _systemUserId, _logger.Object, CancellationToken.None);

        db.ProcessingJobs.Where(j => j.PdfDocumentId == mockId).Should().BeEmpty();
    }

    /// <summary>A Badsworm dogfood demo mock placeholder in Pending state: seed/ prefix, no blob (#3075).</summary>
    private static Guid SeedMockPdf(MeepleAiDbContext db, Guid gameId)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = $"rulebook-{pdfId:N}.pdf",
            FilePath = $"{PdfDocumentEntity.DemoMockFilePathPrefix}badsworm/game-{pdfId:N}/rulebook.pdf",
            ContentHash = "hash",
            UploadedByUserId = Guid.NewGuid(),
            DocumentType = "base",
            DocumentCategory = "Rulebook",
            ProcessingState = nameof(PdfProcessingState.Pending),
        });
        return pdfId;
    }
}
