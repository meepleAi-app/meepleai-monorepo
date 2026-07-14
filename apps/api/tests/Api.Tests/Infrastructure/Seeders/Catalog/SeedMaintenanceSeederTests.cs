using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Models;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

// Issue #2907 (orphan cleanup) + #2908 (stale-Pending re-enqueue): seed-time self-healing.
[Trait("Category", TestCategories.Unit)]
public sealed class SeedMaintenanceSeederTests
{
    private static SharedGameEntity NewGame(string title, int bggId)
        => GameSeeder.CreateFromEnhancedData(
            new SeedManifestGame { Title = title, BggId = bggId, Language = "en", Description = "d" },
            Guid.NewGuid());

    private static PdfDocumentEntity NewPdf(Guid sharedGameId, string fileName, string state = "Pending")
        => new()
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = fileName,
            FilePath = "/tmp/" + fileName,
            FileSizeBytes = 100,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = state,
        };

    private static ProcessingJobEntity NewQueuedJob(Guid pdfId, Guid userId)
        => new()
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            UserId = userId,
            Status = "Queued",
            Priority = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            MaxRetries = 3,
            RetryCount = 0,
        };

    // ── #2907 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CleanupOrphanPdfs_RemovesDanglingPdfs_KeepsValidOnes()
    {
        var dbName = $"seedmaint_cleanup_{Guid.NewGuid():N}";
        var orphanGameId = Guid.NewGuid(); // never inserted into shared_games
        Guid validGameId;

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var game = NewGame("Valid", 13);
            validGameId = game.Id;
            db.SharedGames.Add(game);
            db.PdfDocuments.Add(NewPdf(validGameId, "valid.pdf"));
            db.PdfDocuments.Add(NewPdf(orphanGameId, "orphan.pdf"));
            await db.SaveChangesAsync();
        }

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var removed = await SeedMaintenanceSeeder.CleanupOrphanPdfsAsync(
                db, NullLogger.Instance, CancellationToken.None);
            removed.Should().Be(1);
        }

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var remaining = await db.PdfDocuments.Select(p => p.SharedGameId).ToListAsync();
            remaining.Should().ContainSingle().Which.Should().Be(validGameId);
        }
    }

    [Fact]
    public async Task CleanupOrphanPdfs_NoOrphans_ReturnsZero()
    {
        var dbName = $"seedmaint_clean_noop_{Guid.NewGuid():N}";
        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var game = NewGame("Valid", 42);
            db.SharedGames.Add(game);
            db.PdfDocuments.Add(NewPdf(game.Id, "valid.pdf"));
            await db.SaveChangesAsync();
        }

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var removed = await SeedMaintenanceSeeder.CleanupOrphanPdfsAsync(
                db, NullLogger.Instance, CancellationToken.None);
            removed.Should().Be(0);
        }
    }

    // ── #2908 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReenqueueStalePendingPdfs_EnqueuesOnlyValidPendingWithoutActiveJob()
    {
        var dbName = $"seedmaint_reenqueue_{Guid.NewGuid():N}";
        var systemUserId = Guid.NewGuid();
        var orphanGameId = Guid.NewGuid();
        Guid pendingNoJobId, pendingWithJobId, orphanPendingId, readyId;

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var game = NewGame("Valid", 7);
            db.SharedGames.Add(game);

            var pendingNoJob = NewPdf(game.Id, "a.pdf", "Pending");           // → enqueue
            var pendingWithJob = NewPdf(game.Id, "b.pdf", "Pending");         // has active job → skip
            var orphanPending = NewPdf(orphanGameId, "c.pdf", "Pending");     // orphan → skip
            var ready = NewPdf(game.Id, "d.pdf", "Ready");                    // not Pending → skip
            pendingNoJobId = pendingNoJob.Id;
            pendingWithJobId = pendingWithJob.Id;
            orphanPendingId = orphanPending.Id;
            readyId = ready.Id;
            db.PdfDocuments.AddRange(pendingNoJob, pendingWithJob, orphanPending, ready);
            db.Set<ProcessingJobEntity>().Add(NewQueuedJob(pendingWithJobId, systemUserId));
            await db.SaveChangesAsync();
        }

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var count = await SeedMaintenanceSeeder.ReenqueueStalePendingPdfsAsync(
                db, systemUserId, NullLogger.Instance, CancellationToken.None);
            count.Should().Be(1);
        }

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var queuedPdfIds = await db.Set<ProcessingJobEntity>()
                .Where(j => j.Status == "Queued")
                .Select(j => j.PdfDocumentId)
                .ToListAsync();

            queuedPdfIds.Should().Contain(pendingNoJobId);      // newly enqueued
            queuedPdfIds.Should().Contain(pendingWithJobId);    // its original job (not duplicated)
            queuedPdfIds.Count(id => id == pendingWithJobId).Should().Be(1, "must not double-enqueue");
            queuedPdfIds.Should().NotContain(orphanPendingId);  // orphan skipped
            queuedPdfIds.Should().NotContain(readyId);          // Ready skipped

            // the newly-created job has the 5 pipeline steps
            var newJob = await db.Set<ProcessingJobEntity>()
                .Include(j => j.Steps)
                .FirstAsync(j => j.PdfDocumentId == pendingNoJobId);
            newJob.Steps.Should().HaveCount(5);
        }
    }

    [Fact]
    public async Task ReenqueueStalePendingPdfs_Idempotent_SecondRunEnqueuesNothing()
    {
        var dbName = $"seedmaint_reenqueue_idem_{Guid.NewGuid():N}";
        var systemUserId = Guid.NewGuid();

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            var game = NewGame("Valid", 99);
            db.SharedGames.Add(game);
            db.PdfDocuments.Add(NewPdf(game.Id, "a.pdf", "Pending"));
            await db.SaveChangesAsync();
        }

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            (await SeedMaintenanceSeeder.ReenqueueStalePendingPdfsAsync(
                db, systemUserId, NullLogger.Instance, CancellationToken.None)).Should().Be(1);
        }

        using (var db = TestDbContextFactory.CreateInMemoryDbContext(dbName))
        {
            (await SeedMaintenanceSeeder.ReenqueueStalePendingPdfsAsync(
                db, systemUserId, NullLogger.Instance, CancellationToken.None))
                .Should().Be(0, "the PDF now has an active Queued job");
        }
    }
}
