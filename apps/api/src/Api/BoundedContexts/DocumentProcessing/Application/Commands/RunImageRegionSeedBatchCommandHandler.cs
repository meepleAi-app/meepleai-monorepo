using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #3435 (SP1): batch runner for automatic image-region seeding. For each Ready, never-seeded PDF it
/// runs a dedicated hi_res pass (long-timeout client) and persists Image/FigureCaption regions via
/// <see cref="SeedPdfImageRegionsCommand"/>, then stamps <c>ImageRegionsSeededAt</c> so the PDF is
/// processed exactly once (NFR1). Mirrors <c>BackfillPdfCoversJob</c>: small batch, inter-item delay,
/// per-item try/catch-continue. hi_res is ~200s and NOT reproducible in CI — unit tests drive this
/// with a stubbed <see cref="IRawHiResExtractor"/>; real coverage is a staging run.
/// </summary>
/// <remarks>
/// Per-item isolation (#534 / PR #2830): the inner <see cref="SeedPdfImageRegionsCommand"/> and the
/// marker stamp both SaveChanges on the SAME scoped DbContext, so the loop selects candidates
/// NoTracking, re-loads each PDF AsTracking per item, and calls <c>ChangeTracker.Clear()</c> in a
/// finally — a failed item can never leave a ghost Modified/Added entity that the next item's
/// SaveChanges would re-flush.
/// Deferred to slice 2 (documented follow-ups, not in this slice): the periodic Quartz trigger,
/// a bounded failure-retry / dead-letter cap so a persistently-failing PDF doesn't re-run hi_res
/// forever and starve newer ones (#6), an overlap guard against concurrent triggers (#7), and
/// Prometheus metrics for stuck/failing batches (#8).
/// </remarks>
internal sealed class RunImageRegionSeedBatchCommandHandler
    : ICommandHandler<RunImageRegionSeedBatchCommand, RunImageRegionSeedBatchResult>
{
    internal const int DefaultBatchSize = 3;
    internal const int DefaultDelayBetweenItemsMs = 500;
    internal const string EnabledConfigKey = "PdfProcessing:ImageRegionSeeding:Enabled";
    internal const string BatchSizeConfigKey = "PdfProcessing:ImageRegionSeeding:BatchSize";
    internal const string DelayMsConfigKey = "PdfProcessing:ImageRegionSeeding:DelayMs";

    private readonly MeepleAiDbContext _dbContext;
    private readonly IRawHiResExtractor _rawHiResExtractor;
    private readonly IBlobStorageService _blobStorage;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RunImageRegionSeedBatchCommandHandler> _logger;

    public RunImageRegionSeedBatchCommandHandler(
        MeepleAiDbContext dbContext,
        IRawHiResExtractor rawHiResExtractor,
        IBlobStorageService blobStorage,
        IMediator mediator,
        IConfiguration configuration,
        ILogger<RunImageRegionSeedBatchCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _rawHiResExtractor = rawHiResExtractor ?? throw new ArgumentNullException(nameof(rawHiResExtractor));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RunImageRegionSeedBatchResult> Handle(
        RunImageRegionSeedBatchCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_configuration.GetValue<bool>(EnabledConfigKey))
        {
            _logger.LogInformation(
                "RunImageRegionSeedBatch skipped: feature flag '{Flag}' is disabled", EnabledConfigKey);
            return new RunImageRegionSeedBatchResult(Enabled: false, Processed: 0, TotalRegionsSeeded: 0, Failed: 0);
        }

        var batchSize = command.BatchSize
            ?? _configuration.GetValue<int?>(BatchSizeConfigKey)
            ?? DefaultBatchSize;
        if (batchSize < 1)
        {
            batchSize = DefaultBatchSize;
        }
        var delayMs = _configuration.GetValue<int?>(DelayMsConfigKey) ?? DefaultDelayBetweenItemsMs;

        var readyState = nameof(PdfProcessingState.Ready);
        var demoPrefix = PdfDocumentEntity.DemoMockFilePathPrefix;

        // Select candidates NoTracking (id + path only): the entities are NOT kept tracked across the
        // loop. Each item re-loads its PDF AsTracking and ChangeTracker.Clear()s in a finally, so a
        // failed item can't poison later items via the shared scoped DbContext (#534 / PR #2830).
        // Filter: Ready, never-seeded, in the current KB corpus (IndexerVersion set), excluding
        // demo-mock shells — NFR1: never hi_res text-only/non-corpus PDFs, and never twice.
        var candidates = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.ProcessingState == readyState
                && p.ImageRegionsSeededAt == null
                && p.IndexerVersion != null
                && !p.FilePath.StartsWith(demoPrefix))
            .OrderBy(p => p.UploadedAt)
            .Take(batchSize)
            .Select(p => new { p.Id, p.FilePath })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            _logger.LogDebug("RunImageRegionSeedBatch: no eligible PDFs in queue");
            return new RunImageRegionSeedBatchResult(Enabled: true, Processed: 0, TotalRegionsSeeded: 0, Failed: 0);
        }

        _logger.LogInformation(
            "RunImageRegionSeedBatch: picked up {Count} PDF(s) for hi_res region seeding", candidates.Count);

        var processed = 0;
        var totalSeeded = 0;
        var failed = 0;

        for (var i = 0; i < candidates.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[i];

            try
            {
                var bytes = await LoadPdfBytesAsync(candidate.Id, candidate.FilePath, cancellationToken)
                    .ConfigureAwait(false);
                if (bytes is null)
                {
                    // Transient storage miss — leave unmarked so the next batch retries.
                    _logger.LogWarning(
                        "RunImageRegionSeedBatch: PDF {PdfId} binary not found in blob storage; skipping (will retry)",
                        candidate.Id);
                    failed++;
                    continue;
                }

                string? hiResJson;
                using (var stream = new MemoryStream(bytes))
                {
                    hiResJson = await _rawHiResExtractor
                        .ExtractRawHiResAsync(stream, cancellationToken)
                        .ConfigureAwait(false);
                }

                // Reuse the existing idempotent write path (replace-by-pdf). A region-free PDF
                // (empty/whitespace JSON → 0 regions) is a valid outcome and is still marked below.
                var seeded = await _mediator
                    .Send(new SeedPdfImageRegionsCommand(candidate.Id, hiResJson ?? string.Empty, null), cancellationToken)
                    .ConfigureAwait(false);
                totalSeeded += seeded;

                // Stamp the marker on a freshly-tracked entity (context is clean — cleared each
                // iteration). Even 0 regions is marked so this PDF is not re-processed (NFR1).
                var pdf = await _dbContext.PdfDocuments
                    .AsTracking()
                    .FirstOrDefaultAsync(p => p.Id == candidate.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (pdf is not null)
                {
                    pdf.ImageRegionsSeededAt = DateTime.UtcNow;
                    pdf.UpdatedAt = DateTime.UtcNow;
                    try
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        // ADR-060 Category-B: an admin/re-index mutation won the row during the ~200s
                        // hi_res window. The regions were still persisted by the seed command; leaving
                        // the marker unset means one extra idempotent hi_res pass next run. The finally
                        // below clears the tracker so this conflict cannot leak into the next item.
                        _logger.LogWarning(ex,
                            "RunImageRegionSeedBatch: concurrency conflict stamping seed marker for PDF {PdfId}; " +
                            "regions persisted, marker deferred to next run", candidate.Id);
                    }
                }

                _logger.LogInformation(
                    "RunImageRegionSeedBatch: seeded {Regions} region(s) for PDF {PdfId}", seeded, candidate.Id);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Genuine caller cancellation propagates. A hi_res CLIENT timeout is also an
                // OperationCanceledException but with a different token, so it does NOT match here and
                // falls through to the per-item catch below (mark failed + continue), not an abort.
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // BATCH PATTERN: per-item failures (hi_res timeout/HTTP error, parse) must not abort the
            // batch — log and continue, leaving the PDF unmarked so the next run retries it.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(ex,
                    "RunImageRegionSeedBatch: hi_res region seed failed for PDF {PdfId}; skipping (will retry)",
                    candidate.Id);
                failed++;
            }
            finally
            {
                // Isolate each iteration: detach any entity this item tracked/mutated so a failure
                // (esp. a swallowed marker concurrency conflict) can't be re-flushed by the next item's
                // inner SaveChanges (#534 / PR #2830).
                _dbContext.ChangeTracker.Clear();
            }

            if (i < candidates.Count - 1)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "RunImageRegionSeedBatch complete: processed={Processed}, regionsSeeded={Seeded}, failed={Failed}",
            processed, totalSeeded, failed);

        return new RunImageRegionSeedBatchResult(
            Enabled: true, Processed: processed, TotalRegionsSeeded: totalSeeded, Failed: failed);
    }

    // Mirror of BackfillPdfCoversJob.LoadPdfBytesAsync — R2-only (backfill/seed run in
    // production-like envs where the PDF lives in blob storage). Issue #2671: the blob lives
    // under a random fileId embedded in FilePath, not pdfId.
    private async Task<byte[]?> LoadPdfBytesAsync(Guid pdfId, string filePath, CancellationToken ct)
    {
        var resourceKey = PdfStorageKey.ForPdf(pdfId);
        var fileId = PdfStorageKey.FileIdFromPath(filePath) ?? resourceKey;
        var stream = await _blobStorage.RetrieveAsync(fileId, BlobCategory.Pdf, resourceKey, ct).ConfigureAwait(false);
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
