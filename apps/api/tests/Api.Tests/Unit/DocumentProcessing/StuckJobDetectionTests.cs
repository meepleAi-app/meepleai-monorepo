using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Issue #3585 — "stuck" must mean INACTIVE, not long-running.
/// <para>
/// <c>ProcessingQueueMonitorService</c> measured elapsed time from <c>StartedAt</c> and degraded
/// anything past the threshold to <c>Failed</c>. On staging that classified a healthy ingest of
/// <c>frosthaven_rulebook.pdf</c> — 118 embedding batches, ~40 minutes of real work — as stuck and
/// tried to kill it (it survived only by losing a concurrency race). Worse, the victim then
/// restarted from zero and hit the same wall: no document slower than the recovery timeout could
/// ever finish.
/// </para>
/// <para>
/// The pipeline now stamps <c>LastProgressAt</c> after each embedding batch and the monitor starts
/// its clock from the last sign of life. These tests pin that selection.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3585")]
public sealed class StuckJobDetectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(10);

    /// <summary>Mirrors the monitor's selection predicate (#3585).</summary>
    private static IQueryable<ProcessingJobEntity> StuckJobs(MeepleAiDbContext db)
    {
        var cutoff = Now - StuckThreshold;
        return db.Set<ProcessingJobEntity>()
            .Where(j => j.Status == "Processing"
                     && j.StartedAt != null
                     && (j.LastProgressAt ?? j.StartedAt) < cutoff);
    }

    private static async Task<MeepleAiDbContext> WithJobAsync(
        DateTimeOffset startedAt, DateTimeOffset? lastProgressAt, string status = "Processing")
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "frosthaven_rulebook.pdf",
            FilePath = "pdfs/frosthaven.pdf",
            UploadedByUserId = Guid.NewGuid(),
        });
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            UserId = Guid.NewGuid(),
            Status = status,
            CreatedAt = startedAt,
            StartedAt = startedAt,
            LastProgressAt = lastProgressAt,
        });
        await db.SaveChangesAsync(CancellationToken.None);
        return db;
    }

    [Fact]
    public async Task ALongRunningJobThatKeepsReportingProgress_IsNotStuck()
    {
        // The Frosthaven case: started 40 minutes ago, last batch finished 30 seconds ago.
        using var db = await WithJobAsync(
            startedAt: Now.AddMinutes(-40),
            lastProgressAt: Now.AddSeconds(-30));

        var stuck = await StuckJobs(db).ToListAsync(CancellationToken.None);

        stuck.Should().BeEmpty(
            "a job reporting progress is working, however long it has been running — degrading it "
            + "made documents above the timeout impossible to ingest at all");
    }

    [Fact]
    public async Task AJobThatStoppedReporting_IsStuck()
    {
        // Genuinely hung: last sign of life is older than the threshold.
        using var db = await WithJobAsync(
            startedAt: Now.AddMinutes(-40),
            lastProgressAt: Now.AddMinutes(-25));

        var stuck = await StuckJobs(db).ToListAsync(CancellationToken.None);

        stuck.Should().ContainSingle("silence past the threshold is exactly what the monitor is for");
    }

    [Fact]
    public async Task AJobThatNeverReported_FallsBackToStartedAt()
    {
        // No heartbeat yet (pre-#3585 row, or a job that hung before its first batch).
        using var db = await WithJobAsync(startedAt: Now.AddMinutes(-40), lastProgressAt: null);

        var stuck = await StuckJobs(db).ToListAsync(CancellationToken.None);

        stuck.Should().ContainSingle("without a heartbeat the start time is the only signal available");
    }

    [Fact]
    public async Task AFreshJobWithoutProgressYet_IsNotStuck()
    {
        using var db = await WithJobAsync(startedAt: Now.AddMinutes(-2), lastProgressAt: null);

        var stuck = await StuckJobs(db).ToListAsync(CancellationToken.None);

        stuck.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Queued")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public async Task NonProcessingJobs_AreNeverStuck(string status)
    {
        using var db = await WithJobAsync(
            startedAt: Now.AddMinutes(-90), lastProgressAt: null, status: status);

        var stuck = await StuckJobs(db).ToListAsync(CancellationToken.None);

        stuck.Should().BeEmpty();
    }
}
