# PDF Cover Orphan Recovery Job — Design

**Status**: Spec — pending implementation
**Issue**: #1831 follow-up (deferred from PR #1873)
**Effort estimate**: ~1.5-2h
**Date**: 2026-06-04
**Branch**: `feature/issue-1831-orphan-recovery-followup` (parent: `main-dev`)

## Summary

PR #1873 closed the bulk of #1831 AC but explicitly deferred orphan recovery: PDFs with `CoverGenerationStatus=Generated` and `CoverR2Key` set but the R2 object missing (e.g., deleted out-of-band, R2 bucket lifecycle policy expiry, manual ops mistake). Operators currently must reset manually to `Pending` for re-generation.

This spec introduces a daily Quartz job `PdfCoverOrphanRecoveryJob` that detects orphans (HEAD check via `IBlobStorageService.ExistsAsync`) and resets the entity to `Pending` so the existing `BackfillPdfCoversJob` (every 30min) picks it up for re-generation.

## Goals & Non-goals

### Goals
- Detect orphans automatically without operator intervention
- Reset orphan entity to clean `Pending` state (null all cover fields)
- Re-generation happens via existing `BackfillPdfCoversJob` (no duplication of extraction logic)
- Daily cadence (orphans are rare; no need for hourly scan)

### Non-goals
- Re-generate inline (delegate to `BackfillPdfCoversJob`)
- Detect orphans in real-time (lazy daily check sufficient)
- Manual admin endpoint (potential future, out of scope here)
- Track orphan detection metrics in dedicated table (audit via log only, sufficient for diagnostic)

## Architecture

### File structure
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJob.cs` (~150 LOC)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfCoverOrphanRecoveryJobTests.cs` (~200 LOC, 5 unit tests)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/DocumentProcessingServiceExtensions.cs` (register Quartz job)

### Algorithm

```csharp
public async Task Execute(IJobExecutionContext context)
{
    using var scope = _serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var blob = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
    await RunBatchAsync(db, blob, context.CancellationToken);
}

internal async Task RunBatchAsync(MeepleAiDbContext db, IBlobStorageService blob, CancellationToken ct)
{
    var generatedStatus = nameof(PdfCoverGenerationStatus.Generated);
    
    var batch = await db.PdfDocuments
        .AsTracking()
        .Where(p => p.CoverGenerationStatus == generatedStatus && p.CoverR2Key != null)
        .OrderBy(p => p.UpdatedAt)
        .Take(BatchSize)
        .ToListAsync(ct);
    
    if (batch.Count == 0) return;
    
    var orphanCount = 0;
    foreach (var pdf in batch)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var exists = await blob.ExistsAsync(
                $"{pdf.CoverR2Key}-preview.webp",
                BlobCategory.GameImage,
                pdf.CoverR2Key,
                ct);
            
            if (!exists)
            {
                _logger.LogWarning(
                    "Orphan PDF cover detected, resetting to Pending: PdfDocumentId={Id}, OrphanKey={Key}",
                    pdf.Id, pdf.CoverR2Key);
                
                pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Pending);
                pdf.CoverR2Key = null;
                pdf.CoverGenerationError = null;
                pdf.CoverPageIndex = null;
                orphanCount++;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to check existence for PDF {Id} with key {Key}; skipping in this batch",
                pdf.Id, pdf.CoverR2Key);
        }
        await Task.Delay(DelayBetweenItemsMs, ct);
    }
    
    if (orphanCount > 0)
    {
        await db.SaveChangesAsync(ct);
    }
}
```

### Configuration constants

| Constant | Value | Rationale |
|---|---|---|
| `BatchSize` | 50 | Higher than backfill (5) because HEAD check is faster than extraction; still bounded to prevent runaway scans |
| `DelayBetweenItemsMs` | 1000 | Gentle 1 RPS on blob storage (orphan check is HEAD; cheap) |
| Schedule | `0 0 3 * * ?` Quartz cron | Daily at 03:00 UTC (low traffic) |

### Quartz registration

In `DocumentProcessingServiceExtensions`:
```csharp
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

## Failure modes

| Failure | Recovery |
|---|---|
| `ExistsAsync` throws | Log warning, skip this item, continue batch (best-effort) |
| `SaveChangesAsync` throws | Quartz retries job per `DisallowConcurrentExecution`; orphan detection re-runs next day |
| `OperationCanceledException` | Re-throw to honor cancellation |
| Job runs past 03:00 next day | `DisallowConcurrentExecution` prevents overlap |

## Test plan

5 unit tests in `PdfCoverOrphanRecoveryJobTests`:

1. **`RunBatchAsync_NoGeneratedPdfs_NoOp`** — empty query result → ExistsAsync never called, no SaveChangesAsync
2. **`RunBatchAsync_AllGeneratedExist_NoReset`** — 3 entities, all `ExistsAsync` returns true → 0 resets, no SaveChangesAsync (orphanCount == 0)
3. **`RunBatchAsync_OrphanDetected_ResetsToPending`** — 1 entity, ExistsAsync returns false → entity reset (Pending + nullified fields) + SaveChangesAsync called
4. **`RunBatchAsync_BatchSizeLimit_ProcessesMax50`** — seed 51 entities, only 50 processed (ordering by UpdatedAt)
5. **`RunBatchAsync_ExistsAsyncThrows_LogsAndContinues`** — first item throws, second item processed → no propagation, 1 orphan detected

## Acceptance criteria

- [ ] `PdfCoverOrphanRecoveryJob` Quartz job created with `[DisallowConcurrentExecution]` attribute
- [ ] Daily cron schedule `0 0 3 * * ?` (03:00 UTC)
- [ ] Internal `RunBatchAsync` method for direct unit testing
- [ ] BatchSize=50, DelayBetweenItemsMs=1000 constants
- [ ] Query: `CoverGenerationStatus=Generated AND CoverR2Key NOT NULL`, ordered by `UpdatedAt`
- [ ] Per-item: `ExistsAsync({CoverR2Key}-preview.webp, GameImage, CoverR2Key)` check
- [ ] On orphan: reset `CoverGenerationStatus=Pending`, null `CoverR2Key` + `CoverGenerationError` + `CoverPageIndex`
- [ ] Log warning with PdfDocumentId + orphan key
- [ ] OperationCanceledException re-thrown; other exceptions logged + continue batch
- [ ] 5 unit tests pass (covering AC scenarios)
- [ ] DI registration in `DocumentProcessingServiceExtensions`

## Out of scope (deferred future work)

- Admin on-demand endpoint to trigger orphan recovery manually
- Telemetry dashboard for orphan rate over time
- Bidirectional check (orphan blobs in R2 without corresponding DB entries — would require R2 ListObjects, expensive)
- Multi-region check (single R2 bucket assumed)
- Re-generation inline (delegated to BackfillPdfCoversJob)

## References

- Source issue: [#1831](https://github.com/meepleAi-app/meepleai-monorepo/issues/1831) AC item ⏸ deferred in PR #1873
- Pattern reference: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJob.cs` (mirror Quartz job structure)
- Interface: `IBlobStorageService.ExistsAsync` at `apps/api/src/Api/Services/Pdf/IBlobStorageService.cs:123`
