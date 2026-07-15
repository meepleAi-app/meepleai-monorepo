using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Issue #1831 follow-up — orphan recovery for L4 PDF covers. Detects PDFs
/// with <c>CoverGenerationStatus=Generated</c> whose R2 preview object is
/// missing (deleted out-of-band, R2 lifecycle policy expiry, operator mistake)
/// and resets them to <c>Pending</c> so the existing
/// <see cref="BackfillPdfCoversJob"/> picks them up for re-generation on its
/// next run.
/// </summary>
/// <remarks>
/// <para>Deferred from PR #1873 where the AC item was listed as
/// "Failed is terminal today, manual reset". This job automates the
/// orphan-detection loop without requiring operator intervention.</para>
/// <para>Daily cadence (03:00 UTC) because orphans are rare. The HEAD check
/// via <see cref="IBlobStorageService.GetPresignedUrlForRawKeyAsync"/> is cheap,
/// but bounded by <see cref="BatchSize"/> = 50 per run to avoid runaway scans on
/// very large catalogs. The inter-item sleep of <see cref="DelayBetweenItemsMs"/> = 1000ms
/// keeps the blob storage call rate at ~1 RPS.</para>
/// <para>Issue #2947 fix: the PDF cover R2 key convention became a deterministic,
/// slash-containing key (<c>covers/pdf/{pdfId:D}/cover</c>), which the categorized
/// <see cref="IBlobStorageService.ExistsAsync"/> cannot check — it runs
/// <c>PathSecurity.ValidateIdentifier</c>, which rejects <c>/</c> and <c>.</c>, so it
/// always returned false and the job reset every valid cover. The existence
/// check now mirrors <c>CoverUrlResolver</c> and uses the raw-key primitive.</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class PdfCoverOrphanRecoveryJob : IJob
{
    /// <summary>
    /// Maximum number of PDFs checked in a single job run. Keeps the blob
    /// storage scan rate well below 1 RPS even with the default 1000ms delay.
    /// </summary>
    public const int BatchSize = 50;

    /// <summary>
    /// Sleep between individual existence checks in ms. Throttles the R2 call
    /// rate to roughly 1 RPS to avoid storage quota bursts.
    /// </summary>
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

        // AsTracking: override global NoTracking (PERF-06) so mutations persist.
        // Filter: Generated + non-null CoverR2Key only. OrderBy UpdatedAt for
        // stable pagination across daily runs (oldest checked first).
        var batch = await db.PdfDocuments
            .AsTracking()
            .Where(p => p.CoverGenerationStatus == generatedStatus && p.CoverR2Key != null)
            .OrderBy(p => p.UpdatedAt)
            .Take(BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (batch.Count == 0)
        {
            _logger.LogDebug("PdfCoverOrphanRecoveryJob: no eligible PDFs to check");
            return;
        }

        _logger.LogDebug(
            "PdfCoverOrphanRecoveryJob: checking {Count} Generated PDFs for orphan covers",
            batch.Count);

        var orphanCount = 0;

        for (var i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var pdf = batch[i];

            try
            {
                // Check for the preview variant as the canonical existence signal,
                // via the RAW-KEY primitive (mirrors CoverUrlResolver): CoverR2Key
                // is a deterministic key containing '/' (e.g. "covers/pdf/{id:D}/cover"),
                // which the categorized ExistsAsync cannot validate/resolve.
                var previewRawKey = $"{pdf.CoverR2Key}-preview.webp";
                var exists = await blob.GetPresignedUrlForRawKeyAsync(previewRawKey)
                    .ConfigureAwait(false) is not null;

                if (!exists)
                {
                    _logger.LogWarning(
                        "PdfCoverOrphanRecoveryJob: orphan cover detected — resetting to Pending. " +
                        "PdfDocumentId={Id}, OrphanKey={Key}",
                        pdf.Id, pdf.CoverR2Key);

                    pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Pending);
                    pdf.CoverR2Key = null;
                    pdf.CoverGenerationError = null;
                    pdf.CoverPageIndex = null;
                    pdf.UpdatedAt = DateTime.UtcNow;
                    orphanCount++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // ORPHAN-RECOVERY PATTERN: per-item failures must not abort the batch — log
            // and skip so a single unreachable R2 key doesn't block the full scan.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(ex,
                    "PdfCoverOrphanRecoveryJob: failed to check existence for PDF {Id} with key {Key}; skipping",
                    pdf.Id, pdf.CoverR2Key);
            }

            // Sleep between items, but skip the trailing delay after the last item.
            if (i < batch.Count - 1)
            {
                await Task.Delay(DelayBetweenItemsMs, ct).ConfigureAwait(false);
            }
        }

        if (orphanCount > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation(
                "PdfCoverOrphanRecoveryJob: reset {Count} orphan cover(s) to Pending for re-generation",
                orphanCount);
        }
        else
        {
            _logger.LogDebug("PdfCoverOrphanRecoveryJob: no orphan covers found in this batch");
        }
    }
}
