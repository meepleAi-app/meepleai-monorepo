using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Observability;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #3435 (SP1): batch runner for automatic image-region seeding. For each Ready, never-seeded PDF it
/// runs a dedicated hi_res pass (long-timeout client) and persists Image/FigureCaption/Table regions via
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
/// Slice 2 (this): a periodic Quartz trigger (<c>SeedImageRegionsJob</c>, <c>[DisallowConcurrentExecution]</c>)
/// drives this command automatically; a bounded failed-attempt counter (<see cref="MaxSeedAttempts"/>)
/// dead-letters a persistently-failing PDF so it stops re-running the ~200s hi_res pass and starving
/// newer ones; per-outcome Prometheus metrics (<see cref="MeepleAiMetrics.RecordImageRegionSeed"/>).
/// Still deferred: an overlap guard between a manual admin trigger and the Quartz job (both are
/// idempotent — replace-by-pdf + the marker — so a rare concurrent double-pass only wastes work).
/// </remarks>
internal sealed class RunImageRegionSeedBatchCommandHandler
    : ICommandHandler<RunImageRegionSeedBatchCommand, RunImageRegionSeedBatchResult>
{
    internal const int DefaultBatchSize = 3;
    internal const int DefaultDelayBetweenItemsMs = 500;
    /// <summary>Failed hi_res attempts before a PDF is dead-lettered (excluded from the selector).</summary>
    internal const int MaxSeedAttempts = 3;
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
        // Filter: Ready, never-seeded, still within the retry budget (dead-letter after MaxSeedAttempts),
        // in the current KB corpus (IndexerVersion set), excluding demo-mock shells. NB: this slice has
        // NO image-candidacy pre-filter (the Table-region router is SP2, deferred), so every corpus PDF —
        // text-only included — gets ONE hi_res pass; the marker below guarantees "exactly once". True VLM
        // cost-isolation (never on text-only, NFR1) lands with the router at SP2/SP3.
        var candidates = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.ProcessingState == readyState
                && p.ImageRegionsSeededAt == null
                && p.ImageRegionSeedAttempts < MaxSeedAttempts
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
            var outcome = MeepleAiMetrics.ImageRegionSeedOutcomeFailed;
            var recordMetric = true;

            try
            {
                var bytes = await LoadPdfBytesAsync(candidate.Id, candidate.FilePath, cancellationToken)
                    .ConfigureAwait(false);
                if (bytes is null)
                {
                    // Transient storage miss — count a failed attempt (dead-letter after MaxSeedAttempts).
                    _logger.LogWarning(
                        "RunImageRegionSeedBatch: PDF {PdfId} binary not found in blob storage; skipping (will retry)",
                        candidate.Id);
                    outcome = await RecordSeedFailureAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
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
                        // ADR-060 optimistic concurrency (xmin): an admin/re-index mutation won the row
                        // during the ~200s hi_res window. The seed succeeded (regions persisted); the
                        // marker is deferred to the next idempotent run — a rare, self-resolving retry, so
                        // it is NOT counted as a seed failure. NB: any OTHER marker-save error (deadlock,
                        // constraint, blip) is deliberately NOT caught here — it falls through to the outer
                        // catch → RecordSeedFailureAsync, which bumps the attempt counter so a PERSISTENT
                        // marker failure eventually dead-letters instead of re-running hi_res forever.
                        _logger.LogWarning(ex,
                            "RunImageRegionSeedBatch: concurrency conflict stamping seed marker for PDF {PdfId}; " +
                            "regions persisted, marker deferred to next run", candidate.Id);
                    }
                }

                outcome = seeded > 0
                    ? MeepleAiMetrics.ImageRegionSeedOutcomeSeeded
                    : MeepleAiMetrics.ImageRegionSeedOutcomeEmpty;
                _logger.LogInformation(
                    "RunImageRegionSeedBatch: seeded {Regions} region(s) for PDF {PdfId}", seeded, candidate.Id);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Genuine caller cancellation propagates. A hi_res CLIENT timeout is also an
                // OperationCanceledException but with a different token, so it does NOT match here and
                // falls through to the per-item catch below (mark failed + continue), not an abort.
                // Do NOT emit a 'failed' metric for a cancelled item (it's not a real hi_res failure).
                recordMetric = false;
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // BATCH PATTERN: per-item failures (hi_res timeout/HTTP error, parse) must not abort the
            // batch — log, count a failed attempt, and continue.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(ex,
                    "RunImageRegionSeedBatch: hi_res region seed failed for PDF {PdfId}; skipping (will retry)",
                    candidate.Id);
                outcome = await RecordSeedFailureAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
                failed++;
            }
            finally
            {
                if (recordMetric)
                {
                    MeepleAiMetrics.RecordImageRegionSeed(outcome);
                }
                // Isolate each iteration: detach any entity this item tracked/mutated so a failure
                // (esp. a swallowed marker/attempts concurrency conflict) can't be re-flushed by the
                // next item's inner SaveChanges (#534 / PR #2830).
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

    // Increments the failed-attempt counter for a PDF and returns the metric outcome
    // (dead_letter once it reaches MaxSeedAttempts, else failed). The caller's finally
    // ChangeTracker.Clear() detaches the loaded entity afterwards.
    private async Task<string> RecordSeedFailureAsync(Guid pdfId, CancellationToken ct)
    {
        // Guarantee a clean tracker regardless of what the failing in-try path left pending (e.g. a
        // failed inner seed SaveChanges or a partially-mutated marker entity), so this attempts write
        // never re-flushes another entity's changes (#534 / PR #2830).
        _dbContext.ChangeTracker.Clear();

        var pdf = await _dbContext.PdfDocuments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfId, ct)
            .ConfigureAwait(false);
        if (pdf is null)
        {
            return MeepleAiMetrics.ImageRegionSeedOutcomeFailed;
        }

        pdf.ImageRegionSeedAttempts += 1;
        pdf.UpdatedAt = DateTime.UtcNow;
        var deadLettered = pdf.ImageRegionSeedAttempts >= MaxSeedAttempts;
        try
        {
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // This failure-recording write must NEVER abort the batch (per-item isolation, #534): any DB
            // save error — concurrency, deadlock, constraint, connection blip — is swallowed, so the
            // attempt bump is simply lost and retried on the next run. Mirrors BackfillPdfCoversJob's
            // defensive catch around its own save-of-failure. (A genuine caller cancellation surfaces as
            // OperationCanceledException, not DbUpdateException, so it still propagates.)
            _logger.LogWarning(ex,
                "RunImageRegionSeedBatch: failed to persist attempts bump for PDF {PdfId}", pdfId);
            return MeepleAiMetrics.ImageRegionSeedOutcomeFailed;
        }

        if (deadLettered)
        {
            _logger.LogWarning(
                "RunImageRegionSeedBatch: PDF {PdfId} dead-lettered after {Attempts} failed hi_res attempts",
                pdfId, pdf.ImageRegionSeedAttempts);
            return MeepleAiMetrics.ImageRegionSeedOutcomeDeadLetter;
        }
        return MeepleAiMetrics.ImageRegionSeedOutcomeFailed;
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
