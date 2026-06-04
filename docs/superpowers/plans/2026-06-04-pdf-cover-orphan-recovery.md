# PDF Cover Orphan Recovery Job — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement `PdfCoverOrphanRecoveryJob` Quartz daily job that detects PDFs with `CoverGenerationStatus=Generated` but R2 object missing, and resets them to `Pending` for re-generation via existing `BackfillPdfCoversJob`.

**Architecture:** New Quartz job in `DocumentProcessing.Application.Jobs`. Mirrors `BackfillPdfCoversJob` pattern (DisallowConcurrentExecution, internal RunBatchAsync for testing, scoped DI). Single file + test file + DI registration extension.

**Tech Stack:** .NET 9, Quartz, EF Core, MediatR (not used here), Moq + xUnit for tests.

**Spec reference:** `docs/superpowers/specs/2026-06-04-pdf-cover-orphan-recovery-design.md` (commit `6d77e4780`).

---

## File Structure

### New files
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJob.cs` (~150 LOC)
- `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJobTests.cs` (~200 LOC)

### Modified
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/DocumentProcessingServiceExtensions.cs` (add `RegisterPdfCoverOrphanRecoveryJob` method + invocation)

---

## Task 1: PdfCoverOrphanRecoveryJob + Tests + DI registration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJob.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJobTests.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/DocumentProcessingServiceExtensions.cs`

- [ ] **Step 1: Read reference pattern**

```bash
cat apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJob.cs
cat apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJobTests.cs | head -100
grep -A5 "RegisterPdfProcessingQueueJob\|RegisterBackfillPdfCoversJob" apps/api/src/Api/BoundedContexts/DocumentProcessing/DocumentProcessingServiceExtensions.cs
```

Understand:
- Job structure (DisallowConcurrentExecution attribute, ServiceProvider scope, internal RunBatchAsync)
- DI registration pattern (`RegisterXxxJob` helper)
- Test pattern (Moq for IBlobStorageService, in-memory MeepleAiDbContext)

- [ ] **Step 2: Write the failing test file**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJobTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfCoverOrphanRecoveryJobTests
{
    private readonly Mock<IBlobStorageService> _blob = new();

    [Fact]
    public async Task RunBatchAsync_NoGeneratedPdfs_NoOp()
    {
        await using var db = CreateInMemoryDb();
        var job = CreateJob();

        await job.RunBatchAsync(db, _blob.Object, CancellationToken.None);

        _blob.Verify(b => b.ExistsAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(),
                                         It.IsAny<string>(), It.IsAny<CancellationToken>()),
                     Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_AllGeneratedExist_NoReset()
    {
        await using var db = CreateInMemoryDb();
        SeedPdf(db, "key-1", PdfCoverGenerationStatus.Generated);
        SeedPdf(db, "key-2", PdfCoverGenerationStatus.Generated);
        SeedPdf(db, "key-3", PdfCoverGenerationStatus.Generated);
        await db.SaveChangesAsync();

        _blob.Setup(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                                        It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var job = CreateJob();
        await job.RunBatchAsync(db, _blob.Object, CancellationToken.None);

        var unchanged = await db.PdfDocuments.AsNoTracking().ToListAsync();
        Assert.All(unchanged, p => Assert.Equal(nameof(PdfCoverGenerationStatus.Generated), p.CoverGenerationStatus));
        Assert.All(unchanged, p => Assert.NotNull(p.CoverR2Key));
    }

    [Fact]
    public async Task RunBatchAsync_OrphanDetected_ResetsToPending()
    {
        await using var db = CreateInMemoryDb();
        var orphan = SeedPdf(db, "missing-key", PdfCoverGenerationStatus.Generated);
        orphan.CoverGenerationError = "stale";
        orphan.CoverPageIndex = 2;
        await db.SaveChangesAsync();

        _blob.Setup(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                                        It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);

        var job = CreateJob();
        await job.RunBatchAsync(db, _blob.Object, CancellationToken.None);

        var reset = await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == orphan.Id);
        Assert.Equal(nameof(PdfCoverGenerationStatus.Pending), reset.CoverGenerationStatus);
        Assert.Null(reset.CoverR2Key);
        Assert.Null(reset.CoverGenerationError);
        Assert.Null(reset.CoverPageIndex);
    }

    [Fact]
    public async Task RunBatchAsync_BatchSizeLimit_ProcessesMaxBatchSize()
    {
        await using var db = CreateInMemoryDb();
        // Seed 51 generated PDFs (one more than BatchSize = 50)
        for (var i = 0; i < 51; i++)
        {
            SeedPdf(db, $"key-{i:D2}", PdfCoverGenerationStatus.Generated);
        }
        await db.SaveChangesAsync();

        _blob.Setup(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                                        It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var job = CreateJob();
        await job.RunBatchAsync(db, _blob.Object, CancellationToken.None);

        _blob.Verify(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                                         It.IsAny<string>(), It.IsAny<CancellationToken>()),
                     Times.Exactly(PdfCoverOrphanRecoveryJob.BatchSize));
    }

    [Fact]
    public async Task RunBatchAsync_ExistsAsyncThrows_LogsAndContinuesBatch()
    {
        await using var db = CreateInMemoryDb();
        var first = SeedPdf(db, "throws-key", PdfCoverGenerationStatus.Generated);
        var second = SeedPdf(db, "missing-key", PdfCoverGenerationStatus.Generated);
        await db.SaveChangesAsync();

        _blob.SetupSequence(b => b.ExistsAsync(It.IsAny<string>(), BlobCategory.GameImage,
                                                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("Network error"))
             .ReturnsAsync(false);

        var job = CreateJob();
        await job.RunBatchAsync(db, _blob.Object, CancellationToken.None);

        // First item should remain Generated (skipped due to exception)
        var firstResult = await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == first.Id);
        Assert.Equal(nameof(PdfCoverGenerationStatus.Generated), firstResult.CoverGenerationStatus);

        // Second item should be reset (orphan detected)
        var secondResult = await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == second.Id);
        Assert.Equal(nameof(PdfCoverGenerationStatus.Pending), secondResult.CoverGenerationStatus);
        Assert.Null(secondResult.CoverR2Key);
    }

    // === Helpers ===

    private static MeepleAiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MeepleAiDbContext(options);
    }

    private static PdfDocumentEntity SeedPdf(MeepleAiDbContext db, string coverKey, PdfCoverGenerationStatus status)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FilePath = "test/path.pdf",
            FileSize = 1000,
            OriginalFileName = "test.pdf",
            ContentType = "application/pdf",
            UploadedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ProcessingState = nameof(PdfProcessingState.Ready),
            CoverGenerationStatus = nameof(status),
            CoverR2Key = coverKey,
        };
        db.PdfDocuments.Add(pdf);
        return pdf;
    }

    private static PdfCoverOrphanRecoveryJob CreateJob()
    {
        // ServiceProvider is unused by RunBatchAsync; pass null-equivalent
        var sp = new Mock<IServiceProvider>().Object;
        return new PdfCoverOrphanRecoveryJob(sp, NullLogger<PdfCoverOrphanRecoveryJob>.Instance);
    }
}
```

**IMPORTANT before writing**: Read the existing `BackfillPdfCoversJobTests.cs` test setup to verify if `MeepleAiDbContext` In-Memory DB pattern is OK or if Testcontainers Postgres is required (HasQueryFilter / specific Postgres functions may not work in-memory).

If In-Memory fails due to EF Core feature constraints, mock `IPdfDocumentRepository` instead (if exists) or use the same pattern from BackfillPdfCoversJobTests.

- [ ] **Step 3: Run test to verify failure**

```bash
dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~PdfCoverOrphanRecoveryJobTests" --no-build 2>&1 | tail -10
```

Expected: COMPILE ERROR — `PdfCoverOrphanRecoveryJob` doesn't exist yet.

- [ ] **Step 4: Implement PdfCoverOrphanRecoveryJob**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJob.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Issue #1831 follow-up — orphan recovery for L4 PDF covers. Detects PDFs
/// with <c>CoverGenerationStatus=Generated</c> but the R2 object missing
/// (deleted out-of-band, R2 lifecycle policy expiry, manual ops mistake) and
/// resets them to <c>Pending</c> so the existing
/// <see cref="BackfillPdfCoversJob"/> picks them up for re-generation.
/// </summary>
/// <remarks>
/// <para>Deferred from PR #1873 where the AC item ⏸ was listed as
/// "Failed is terminal today, manual reset". This job automates that
/// orphan-detection loop without operator intervention.</para>
/// <para>Daily cadence (03:00 UTC) because orphans are rare. The HEAD check
/// via <see cref="IBlobStorageService.ExistsAsync"/> is cheap, but bounded by
/// <see cref="BatchSize"/> = 50 per run to avoid runaway scans on very large
/// catalogs. Inter-item sleep of <see cref="DelayBetweenItemsMs"/> = 1000ms
/// keeps the blob storage call rate at ~1 RPS.</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class PdfCoverOrphanRecoveryJob : IJob
{
    public const int BatchSize = 50;
    public const int DelayBetweenItemsMs = 1000;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PdfCoverOrphanRecoveryJob> _logger;

    public PdfCoverOrphanRecoveryJob(
        IServiceProvider serviceProvider,
        ILogger<PdfCoverOrphanRecoveryJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("PdfCoverOrphanRecoveryJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var blob = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();

        await RunBatchAsync(db, blob, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal batch runner — extracted from <see cref="Execute"/> so unit
    /// tests can drive it directly without spinning up the Quartz scheduler.
    /// </summary>
    internal async Task RunBatchAsync(
        MeepleAiDbContext db,
        IBlobStorageService blob,
        CancellationToken ct)
    {
        var generatedStatus = nameof(PdfCoverGenerationStatus.Generated);

        var batch = await db.PdfDocuments
            .AsTracking()
            .Where(p => p.CoverGenerationStatus == generatedStatus && p.CoverR2Key != null)
            .OrderBy(p => p.UpdatedAt)
            .Take(BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (batch.Count == 0)
        {
            _logger.LogDebug("PdfCoverOrphanRecoveryJob: no eligible PDFs in queue");
            return;
        }

        var orphanCount = 0;
        for (var i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var pdf = batch[i];
            try
            {
                var exists = await blob.ExistsAsync(
                    $"{pdf.CoverR2Key}-preview.webp",
                    BlobCategory.GameImage,
                    pdf.CoverR2Key!,
                    ct).ConfigureAwait(false);

                if (!exists)
                {
                    _logger.LogWarning(
                        "Orphan PDF cover detected, resetting to Pending: PdfDocumentId={Id}, OrphanKey={Key}",
                        pdf.Id, pdf.CoverR2Key);

                    pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Pending);
                    pdf.CoverR2Key = null;
                    pdf.CoverGenerationError = null;
                    pdf.CoverPageIndex = null;
                    pdf.UpdatedAt = DateTimeOffset.UtcNow;
                    orphanCount++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to check existence for PDF {Id} with key {Key}; skipping in this batch",
                    pdf.Id, pdf.CoverR2Key);
            }

            // Sleep between items (skip after last item to avoid trailing delay)
            if (i < batch.Count - 1)
            {
                await Task.Delay(DelayBetweenItemsMs, ct).ConfigureAwait(false);
            }
        }

        if (orphanCount > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("PdfCoverOrphanRecoveryJob: reset {Count} orphan covers to Pending", orphanCount);
        }
    }
}
```

- [ ] **Step 5: Register Quartz job in DocumentProcessingServiceExtensions**

Find the existing `RegisterBackfillPdfCoversJob` private method in `DocumentProcessingServiceExtensions.cs` and add a sibling method `RegisterPdfCoverOrphanRecoveryJob`, then invoke it in the configurator block (same place as the backfill registration).

```csharp
// In DocumentProcessingServiceExtensions.cs (add new method + invoke):

private static void RegisterPdfCoverOrphanRecoveryJob(IServiceCollectionQuartzConfigurator quartz)
{
    var jobKey = new JobKey(nameof(PdfCoverOrphanRecoveryJob));
    quartz.AddJob<PdfCoverOrphanRecoveryJob>(opts => opts.WithIdentity(jobKey));
    quartz.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity($"{nameof(PdfCoverOrphanRecoveryJob)}-trigger")
        .WithCronSchedule("0 0 3 * * ?")); // Daily at 03:00 UTC
}
```

And in the existing Quartz configurator block, after `RegisterBackfillPdfCoversJob(q);`:
```csharp
RegisterPdfCoverOrphanRecoveryJob(q);
```

- [ ] **Step 6: Run build + tests**

```bash
dotnet build apps/api/src/Api 2>&1 | tail -5
# Expected: BUILD SUCCEEDED

dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~PdfCoverOrphanRecoveryJobTests" 2>&1 | tail -15
# Expected: 5/5 PASS
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJob.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/DocumentProcessingServiceExtensions.cs \
        apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJobTests.cs
git commit -m "feat(document-processing): #1831 follow-up PdfCoverOrphanRecoveryJob daily orphan detection"
```

## Self-Review Checklist

- [ ] Job has `[DisallowConcurrentExecution]` attribute
- [ ] Constants `BatchSize = 50`, `DelayBetweenItemsMs = 1000`
- [ ] Internal `RunBatchAsync` method (testable without Quartz scheduler)
- [ ] Query filters on `CoverGenerationStatus = Generated AND CoverR2Key != null`
- [ ] OrderBy `UpdatedAt` for stable ordering
- [ ] `ExistsAsync` called with correct fileId (`{CoverR2Key}-preview.webp`), category `GameImage`, resourceKey (`CoverR2Key`)
- [ ] On orphan: reset all 4 cover fields + UpdatedAt
- [ ] `OperationCanceledException` re-thrown; other exceptions logged + continue
- [ ] Sleep between items (skip after last)
- [ ] SaveChangesAsync only if orphanCount > 0 (avoid empty save)
- [ ] Log warning per orphan + info summary after batch
- [ ] Quartz registered with daily 03:00 UTC cron
- [ ] 5 unit tests cover all spec scenarios
- [ ] Build + tests pass

## Spec coverage check (all AC mapped)

| AC | Task step |
|---|---|
| Quartz job with DisallowConcurrentExecution | Step 4 |
| Daily cron 03:00 UTC | Step 5 |
| Internal RunBatchAsync | Step 4 |
| BatchSize=50, Delay=1000 | Step 4 |
| Query filter + ordering | Step 4 |
| ExistsAsync per-item | Step 4 |
| Reset fields on orphan | Step 4 |
| Log warning + info | Step 4 |
| Exception handling | Step 4 |
| 5 unit tests | Step 2 |
| DI registration | Step 5 |
