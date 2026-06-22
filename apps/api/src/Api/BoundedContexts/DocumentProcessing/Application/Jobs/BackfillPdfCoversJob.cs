using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Issue #1831 AC — backfill job for L4 PDF cover extraction. Picks up PDFs
/// whose pipeline has completed (<c>ProcessingState=Ready</c>) but whose cover
/// has not yet been generated (<c>CoverGenerationStatus=Pending</c>), runs the
/// same extractor + upload flow as the pipeline, and emits
/// <see cref="PdfCoverGeneratedEvent"/> on success so SharedGame.PdfCoverR2Key
/// propagation (#1852) fires.
/// </summary>
/// <remarks>
/// <para>Use case: PDFs uploaded before the L4 cover stack shipped (PR #1839 /
/// #1843, merged 2026-06-02) have <c>CoverGenerationStatus=Pending</c>
/// permanently because the cover extraction step did not exist at upload time.
/// This job lazily backfills them without re-running the full ingestion
/// pipeline.</para>
/// <para>Gentle by design: batch of 5 per run, 500ms inter-item sleep, runs
/// every 30 minutes. A failed extraction lands in terminal
/// <c>CoverGenerationStatus=Failed</c> and is not retried — operators reset to
/// <c>Pending</c> manually if they want a fresh attempt after addressing the
/// root cause.</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class BackfillPdfCoversJob : IJob
{
    /// <summary>
    /// Maximum number of PDFs processed in a single job run. Keeps the
    /// backfill rate well below 1 RPS even with fast extraction (extractor
    /// runs in &lt; 1s on typical rulebooks).
    /// </summary>
    public const int BatchSize = 5;

    /// <summary>
    /// Sleep between individual PDFs in a single batch run, to avoid bursting
    /// the blob storage and extractor with concurrent work.
    /// </summary>
    public const int DelayBetweenItemsMs = 500;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackfillPdfCoversJob> _logger;

    public BackfillPdfCoversJob(IServiceProvider serviceProvider, ILogger<BackfillPdfCoversJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("BackfillPdfCoversJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var extractor = scope.ServiceProvider.GetRequiredService<IPdfCoverExtractor>();
        var blob = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        var eventCollector = scope.ServiceProvider.GetService<IDomainEventCollector>();

        await RunBatchAsync(db, extractor, blob, eventCollector, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal batch runner — extracted from <see cref="Execute"/> so unit
    /// tests can drive it directly without spinning up the Quartz scheduler.
    /// </summary>
    internal async Task RunBatchAsync(
        MeepleAiDbContext db,
        IPdfCoverExtractor extractor,
        IBlobStorageService blob,
        IDomainEventCollector? eventCollector,
        CancellationToken ct)
    {
        var pendingStatus = nameof(PdfCoverGenerationStatus.Pending);
        var readyState = nameof(PdfProcessingState.Ready);

        // AsTracking: Override global NoTracking (PERF-06) so SaveChangesAsync persists mutations.
        var batch = await db.PdfDocuments
            .AsTracking()
            .Where(p => p.CoverGenerationStatus == pendingStatus && p.ProcessingState == readyState)
            .OrderBy(p => p.UploadedAt)
            .Take(BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (batch.Count == 0)
        {
            _logger.LogDebug("BackfillPdfCoversJob: no eligible PDFs in queue");
            return;
        }

        _logger.LogInformation(
            "BackfillPdfCoversJob: picked up {Count} PDFs for cover backfill",
            batch.Count);

        for (var i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var pdf = batch[i];
            await ProcessOneAsync(pdf, db, extractor, blob, eventCollector, ct).ConfigureAwait(false);

            if (i < batch.Count - 1)
            {
                await Task.Delay(DelayBetweenItemsMs, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessOneAsync(
        PdfDocumentEntity pdf,
        MeepleAiDbContext db,
        IPdfCoverExtractor extractor,
        IBlobStorageService blob,
        IDomainEventCollector? eventCollector,
        CancellationToken ct)
    {
        try
        {
            var pdfBytes = await LoadPdfBytesAsync(pdf, blob, ct).ConfigureAwait(false);
            if (pdfBytes is null)
            {
                pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Failed);
                pdf.CoverGenerationError = "PDF binary not found in blob storage";
                _logger.LogWarning(
                    "BackfillPdfCoversJob: PDF {PdfId} binary missing from blob storage; marking Failed",
                    pdf.Id);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return;
            }

            var result = await extractor.ExtractAsync(pdfBytes, ct).ConfigureAwait(false);

            switch (result.Outcome)
            {
                case PdfCoverExtractionOutcome.Generated:
                    {
                        var resourceKey = $"pdf-cover-{pdf.Id}";

                        using (var thumbStream = new MemoryStream(result.ThumbnailWebp!, writable: false))
                        {
                            await blob.StoreAsync(thumbStream, "thumb.webp", BlobCategory.GameImage, resourceKey, ct)
                                .ConfigureAwait(false);
                        }
                        using (var previewStream = new MemoryStream(result.PreviewWebp!, writable: false))
                        {
                            await blob.StoreAsync(previewStream, "preview.webp", BlobCategory.GameImage, resourceKey, ct)
                                .ConfigureAwait(false);
                        }

                        pdf.CoverR2Key = resourceKey;
                        pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Generated);
                        pdf.CoverPageIndex = result.SelectedPageIndex;
                        pdf.CoverGenerationError = null;

                        eventCollector?.Collect(new PdfCoverGeneratedEvent(
                            pdfDocumentId: pdf.Id,
                            sharedGameId: pdf.SharedGameId,
                            coverR2Key: resourceKey,
                            coverPageIndex: result.SelectedPageIndex ?? 0));

                        _logger.LogInformation(
                            "BackfillPdfCoversJob: cover generated for PDF {PdfId} from page {PageIndex}",
                            pdf.Id, result.SelectedPageIndex);
                        break;
                    }
                case PdfCoverExtractionOutcome.Skipped:
                    pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Skipped);
                    pdf.CoverPageIndex = result.SelectedPageIndex;
                    _logger.LogInformation(
                        "BackfillPdfCoversJob: cover skipped for PDF {PdfId} (heuristic rejected all candidate pages)",
                        pdf.Id);
                    break;
                case PdfCoverExtractionOutcome.Failed:
                    pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Failed);
                    pdf.CoverGenerationError = result.ErrorMessage;
                    _logger.LogWarning(
                        "BackfillPdfCoversJob: cover extraction failed for PDF {PdfId}: {Error}",
                        pdf.Id, result.ErrorMessage);
                    break;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKFILL PATTERN: per-item failures must not abort the batch — log,
        // mark the entity Failed, and continue with the next PDF so a single
        // pathological document doesn't stall the queue.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Operator-recovery hint: when StoreAsync succeeded for one or both
            // sizes BUT the subsequent SaveChangesAsync threw (transient DB
            // error, RowVersion conflict, etc.), R2 will contain orphan
            // thumb.webp / preview.webp under "game-images/pdf-cover-{Id}/".
            // The entity ends up Failed without a CoverR2Key so
            // PdfDeletedEventHandler can't reach them. We embed the prospective
            // resourceKey in CoverGenerationError so operators can grep the bucket.
            var resourceKey = $"pdf-cover-{pdf.Id}";
            _logger.LogWarning(ex,
                "BackfillPdfCoversJob: unexpected error processing PDF {PdfId}; marking Failed. " +
                "Inspect R2 prefix game-images/{ResourceKey}/ for orphan blobs and clean up manually if present.",
                pdf.Id, resourceKey);
            pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Failed);
            var detail = ex.GetType().Name + ": orphan-check-key=" + resourceKey;
            pdf.CoverGenerationError = detail.Length > 500 ? detail[..500] : detail;
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "BackfillPdfCoversJob: failed to persist Failed status for PDF {PdfId}",
                    pdf.Id);
            }
        }
    }

    private static async Task<byte[]?> LoadPdfBytesAsync(
        PdfDocumentEntity pdf,
        IBlobStorageService blob,
        CancellationToken ct)
    {
        // Mirror of PdfProcessingPipelineService.LoadPdfBytesAsync but R2-only:
        // backfill is intended for production-like environments where the PDF
        // lives in blob storage. The filesystem fallback in the pipeline
        // service exists for dev/test, which doesn't need a backfill job.
        var fileId = PdfStorageKey.ForPdf(pdf.Id);
        var stream = await blob.RetrieveAsync(fileId, BlobCategory.Pdf, fileId, ct).ConfigureAwait(false);
        if (stream is null)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, ct).ConfigureAwait(false);
            return memoryStream.ToArray();
        }
    }
}
