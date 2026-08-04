using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Observability;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #3435 (SP4): batch runner for the async VLM table-extraction pass. Consumes SP2's router to pick
/// candidate PDFs, then for each PENDING image region: renders + crops the page (Docnet/SkiaSharp),
/// posts the crop to the smoldocling crop-discriminator, and — for OTSL tables — persists a
/// retrievable table chunk. Per-region state (idempotency + retry-cap/dead-letter) lives in
/// <c>pdf_table_extractions</c>, keyed by a bbox-stable region hash. Mirrors the batch/isolation
/// pattern of <see cref="RunImageRegionSeedBatchCommandHandler"/> (per-item try/catch-continue +
/// <c>ChangeTracker.Clear()</c>). Flag-gated by <c>PdfProcessing:TableExtraction:Enabled</c>.
/// </summary>
internal sealed class RunTableExtractionBatchCommandHandler
    : ICommandHandler<RunTableExtractionBatchCommand, RunTableExtractionBatchResult>
{
    internal const int DefaultBatchSize = 10;
    internal const int DefaultDelayBetweenItemsMs = 200;
    /// <summary>Failed VLM attempts on one region before it is dead-lettered (excluded from the selector).</summary>
    internal const int DefaultMaxAttempts = 3;
    internal const string EnabledConfigKey = "PdfProcessing:TableExtraction:Enabled";
    internal const string BatchSizeConfigKey = "PdfProcessing:TableExtraction:BatchSize";
    internal const string DelayMsConfigKey = "PdfProcessing:TableExtraction:DelayMs";
    internal const string MaxAttemptsConfigKey = "PdfProcessing:TableExtraction:MaxAttempts";

    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly IPdfRegionCropper _cropper;
    private readonly ISmolDoclingTableExtractor _extractor;
    private readonly ITableChunkIndexer _indexer;
    private readonly IBlobStorageService _blobStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RunTableExtractionBatchCommandHandler> _logger;

    public RunTableExtractionBatchCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        IPdfRegionCropper cropper,
        ISmolDoclingTableExtractor extractor,
        ITableChunkIndexer indexer,
        IBlobStorageService blobStorage,
        IConfiguration configuration,
        ILogger<RunTableExtractionBatchCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _cropper = cropper ?? throw new ArgumentNullException(nameof(cropper));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RunTableExtractionBatchResult> Handle(
        RunTableExtractionBatchCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_configuration.GetValue<bool>(EnabledConfigKey))
        {
            _logger.LogInformation(
                "RunTableExtractionBatch skipped: feature flag '{Flag}' is disabled", EnabledConfigKey);
            return new RunTableExtractionBatchResult(Enabled: false, Processed: 0, Extracted: 0, NotTable: 0, Failed: 0);
        }

        var regionBudget = command.BatchSize
            ?? _configuration.GetValue<int?>(BatchSizeConfigKey)
            ?? DefaultBatchSize;
        if (regionBudget < 1)
        {
            regionBudget = DefaultBatchSize;
        }
        var delayMs = _configuration.GetValue<int?>(DelayMsConfigKey) ?? DefaultDelayBetweenItemsMs;
        var maxAttempts = _configuration.GetValue<int?>(MaxAttemptsConfigKey) ?? DefaultMaxAttempts;

        // Consume SP2's router (its own eligibility + threshold config) to pick candidate PDFs.
        var candidates = await _mediator
            .Send(new GetTableRegionCandidatesQuery(), cancellationToken)
            .ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            _logger.LogDebug("RunTableExtractionBatch: no candidate PDFs from the table-region router");
            return new RunTableExtractionBatchResult(Enabled: true, Processed: 0, Extracted: 0, NotTable: 0, Failed: 0);
        }

        var processed = 0;
        var extracted = 0;
        var notTable = 0;
        var failed = 0;

        foreach (var candidate in candidates)
        {
            if (processed >= regionBudget)
            {
                break;
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Load PDF context (NoTracking — the indexer reads it, nothing mutates it) + its regions
            // and the already-recorded extraction states for this PDF.
            var pdf = await _dbContext.PdfDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == candidate.PdfDocumentId, cancellationToken)
                .ConfigureAwait(false);
            if (pdf is null)
            {
                continue;
            }

            var regions = await _dbContext.PdfImageRegions
                .AsNoTracking()
                .Where(r => r.PdfDocumentId == candidate.PdfDocumentId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (regions.Count == 0)
            {
                continue;
            }

            var states = await _dbContext.PdfTableExtractions
                .AsNoTracking()
                .Where(e => e.PdfDocumentId == candidate.PdfDocumentId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            // Region hashes are unique per (pdf, region) via the ux_pdf_table_extractions_pdf_region
            // unique index, so a plain keyed dictionary is safe.
            var stateByHash = states.ToDictionary(s => s.RegionHash, StringComparer.Ordinal);

            // A document reindex (ReindexDocumentCommandHandler / the pipeline) deletes this PDF's
            // text_chunks + embeddings WITHOUT clearing this sidecar, so an 'extracted' region is only
            // truly done if its produced chunk still exists. Load the still-live table-chunk ids so a
            // wiped chunk is re-indexed (from cached markdown) instead of skipped forever (#3435 SP4 review).
            var extractedChunkIds = states
                .Where(s => string.Equals(s.Status, PdfTableExtractionEntity.StatusExtracted, StringComparison.Ordinal)
                    && s.TextChunkId.HasValue)
                .Select(s => s.TextChunkId!.Value)
                .Distinct()
                .ToList();
            var liveChunkIds = extractedChunkIds.Count == 0
                ? new HashSet<Guid>()
                : (await _dbContext.TextChunks
                    .AsNoTracking()
                    .Where(tc => extractedChunkIds.Contains(tc.Id))
                    .Select(tc => tc.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)).ToHashSet();

            byte[]? pdfBytes = null;
            var pdfBytesLoaded = false;

            foreach (var region in regions)
            {
                if (processed >= regionBudget)
                {
                    break;
                }
                cancellationToken.ThrowIfCancellationRequested();

                var regionHash = TableRegionKey.ComputeRegionHash(
                    pdf.Id, region.PageNumber, region.X, region.Y, region.Width, region.Height);

                // Skip only regions that are terminal AND (for 'extracted') whose produced chunk still
                // exists — a chunk wiped by a reindex must be re-indexed below, not skipped.
                stateByHash.TryGetValue(regionHash, out var state);
                if (state is not null && IsTerminalAndLive(state, maxAttempts, liveChunkIds))
                {
                    continue;
                }

                processed++;
                var outcome = MeepleAiMetrics.TableVlmOutcomeFailed;
                var recordMetric = true;

                try
                {
                    string markdown;
                    double? confidence;
                    string? reason;

                    // Self-heal: an 'extracted' region whose chunk was wiped by a reindex still has the
                    // cached markdown — re-index it WITHOUT re-running crop/VLM (cheap, correct on bulk
                    // reindex where re-running the VLM over the whole corpus would be wasteful).
                    if (state is not null
                        && string.Equals(state.Status, PdfTableExtractionEntity.StatusExtracted, StringComparison.Ordinal)
                        && !string.IsNullOrEmpty(state.TableMarkdown))
                    {
                        markdown = state.TableMarkdown!;
                        confidence = state.Confidence;
                        reason = state.Reason;
                    }
                    else
                    {
                        if (!pdfBytesLoaded)
                        {
                            pdfBytes = await LoadPdfBytesAsync(pdf.Id, pdf.FilePath, cancellationToken).ConfigureAwait(false);
                            pdfBytesLoaded = true;
                        }
                        if (pdfBytes is null)
                        {
                            _logger.LogWarning(
                                "RunTableExtractionBatch: PDF {PdfId} binary not found in blob storage; skipping (will retry)",
                                pdf.Id);
                            outcome = await RecordFailureAsync(pdf.Id, region, regionHash, "pdf binary not found", maxAttempts, cancellationToken)
                                .ConfigureAwait(false);
                            failed++;
                            break; // a missing blob affects every region of this PDF
                        }

                        var cropBytes = _cropper.CropRegion(
                            pdfBytes, region.PageNumber, region.X, region.Y, region.Width, region.Height, cancellationToken);
                        if (cropBytes is null)
                        {
                            outcome = await RecordFailureAsync(pdf.Id, region, regionHash, "region crop failed", maxAttempts, cancellationToken)
                                .ConfigureAwait(false);
                            failed++;
                            continue;
                        }

                        var vlm = await _extractor.ExtractTableAsync(cropBytes, prefilter: null, cancellationToken).ConfigureAwait(false);
                        if (!vlm.IsTable)
                        {
                            await MarkTerminalAsync(
                                pdf.Id, region, regionHash, PdfTableExtractionEntity.StatusNotTable,
                                markdown: null, confidence: vlm.Confidence, reason: vlm.Reason, textChunkId: null, cancellationToken)
                                .ConfigureAwait(false);
                            outcome = MeepleAiMetrics.TableVlmOutcomeNotTable;
                            notTable++;
                            continue;
                        }
                        markdown = vlm.Markdown;
                        confidence = vlm.Confidence;
                        reason = vlm.Reason;
                    }

                    var chunkId = await _indexer.IndexTableAsync(
                        pdf, region.PageNumber, region.X, region.Y, region.Width, region.Height,
                        markdown, regionHash, cancellationToken).ConfigureAwait(false);
                    if (chunkId is null)
                    {
                        // Confirmed a table but not retrievable yet (no game / vector document) — a transient
                        // condition, so retry (and eventually dead-letter), NEVER mark terminal 'extracted'.
                        outcome = await RecordFailureAsync(
                            pdf.Id, region, regionHash, "table not retrievable (no game/vector document)", maxAttempts, cancellationToken)
                            .ConfigureAwait(false);
                        failed++;
                        continue;
                    }

                    await MarkTerminalAsync(
                        pdf.Id, region, regionHash, PdfTableExtractionEntity.StatusExtracted,
                        markdown: markdown, confidence: confidence, reason: reason, textChunkId: chunkId, cancellationToken)
                        .ConfigureAwait(false);
                    outcome = MeepleAiMetrics.TableVlmOutcomeExtracted;
                    extracted++;
                    _logger.LogInformation(
                        "RunTableExtractionBatch: extracted table for PDF {PdfId} page {Page} (chunk {ChunkId})",
                        pdf.Id, region.PageNumber, chunkId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    recordMetric = false;
                    throw;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogWarning(ex,
                        "RunTableExtractionBatch: table extraction failed for PDF {PdfId} page {Page}; skipping (will retry)",
                        pdf.Id, region.PageNumber);
                    outcome = await RecordFailureAsync(pdf.Id, region, regionHash, ex.Message, maxAttempts, cancellationToken)
                        .ConfigureAwait(false);
                    failed++;
                }
                finally
                {
                    if (recordMetric)
                    {
                        MeepleAiMetrics.RecordTableVlm(outcome);
                    }
                    // Isolate each region: detach any entity this item tracked/mutated so a failure
                    // can't be re-flushed by the next item's SaveChanges (#534 pattern).
                    _dbContext.ChangeTracker.Clear();
                }

                if (processed < regionBudget)
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        _logger.LogInformation(
            "RunTableExtractionBatch complete: processed={Processed}, extracted={Extracted}, notTable={NotTable}, failed={Failed}",
            processed, extracted, notTable, failed);

        return new RunTableExtractionBatchResult(
            Enabled: true, Processed: processed, Extracted: extracted, NotTable: notTable, Failed: failed);
    }

    private static bool IsTerminal(PdfTableExtractionEntity state, int maxAttempts) =>
        state.Status is PdfTableExtractionEntity.StatusExtracted
            or PdfTableExtractionEntity.StatusNotTable
            or PdfTableExtractionEntity.StatusDeadLetter
        || (string.Equals(state.Status, PdfTableExtractionEntity.StatusFailed, StringComparison.Ordinal)
            && state.Attempts >= maxAttempts);

    // A region is only truly done if it is terminal AND — when it is 'extracted' — its produced chunk
    // still exists. A document reindex wipes text_chunks/embeddings without clearing this sidecar, so an
    // 'extracted' row whose chunk is gone must be re-indexed rather than skipped (#3435 SP4 review).
    private static bool IsTerminalAndLive(PdfTableExtractionEntity state, int maxAttempts, HashSet<Guid> liveChunkIds)
    {
        if (!IsTerminal(state, maxAttempts))
        {
            return false;
        }
        if (string.Equals(state.Status, PdfTableExtractionEntity.StatusExtracted, StringComparison.Ordinal))
        {
            return state.TextChunkId is Guid chunkId && liveChunkIds.Contains(chunkId);
        }
        return true; // not_table / dead_letter / failed-at-cap remain terminal
    }

    private async Task MarkTerminalAsync(
        Guid pdfId, PdfImageRegionEntity region, string regionHash, string status,
        string? markdown, double? confidence, string? reason, Guid? textChunkId, CancellationToken ct)
    {
        var entity = await LoadOrCreateTrackedAsync(pdfId, region, regionHash, ct).ConfigureAwait(false);
        entity.Status = status;
        entity.TableMarkdown = markdown;
        entity.Confidence = confidence;
        entity.Reason = Truncate(reason, 32);
        entity.TextChunkId = textChunkId;
        entity.LastError = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await SaveSidecarAsync(pdfId, ct).ConfigureAwait(false);
    }

    private async Task<string> RecordFailureAsync(
        Guid pdfId, PdfImageRegionEntity region, string regionHash, string error, int maxAttempts, CancellationToken ct)
    {
        var entity = await LoadOrCreateTrackedAsync(pdfId, region, regionHash, ct).ConfigureAwait(false);
        entity.Attempts += 1;
        entity.LastError = Truncate(error, 500);
        entity.UpdatedAt = DateTime.UtcNow;
        var deadLettered = entity.Attempts >= maxAttempts;
        entity.Status = deadLettered
            ? PdfTableExtractionEntity.StatusDeadLetter
            : PdfTableExtractionEntity.StatusFailed;
        await SaveSidecarAsync(pdfId, ct).ConfigureAwait(false);
        return deadLettered
            ? MeepleAiMetrics.TableVlmOutcomeDeadLetter
            : MeepleAiMetrics.TableVlmOutcomeFailed;
    }

    private async Task<PdfTableExtractionEntity> LoadOrCreateTrackedAsync(
        Guid pdfId, PdfImageRegionEntity region, string regionHash, CancellationToken ct)
    {
        // Start from a clean tracker so this write never re-flushes a prior item's pending changes.
        _dbContext.ChangeTracker.Clear();
        var entity = await _dbContext.PdfTableExtractions
            .AsTracking()
            .FirstOrDefaultAsync(e => e.PdfDocumentId == pdfId && e.RegionHash == regionHash, ct)
            .ConfigureAwait(false);
        if (entity is null)
        {
            entity = new PdfTableExtractionEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                RegionHash = regionHash,
                PageNumber = region.PageNumber,
                X = region.X,
                Y = region.Y,
                Width = region.Width,
                Height = region.Height,
                Status = PdfTableExtractionEntity.StatusPending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _dbContext.PdfTableExtractions.Add(entity);
        }
        return entity;
    }

    private async Task SaveSidecarAsync(Guid pdfId, CancellationToken ct)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Per-item isolation: a sidecar write must never abort the batch. The state update is
            // lost and reconstructed on the next run (the region simply reprocesses).
            _logger.LogWarning(ex,
                "RunTableExtractionBatch: failed to persist table-extraction state for PDF {PdfId}", pdfId);
        }
    }

    // Mirror of RunImageRegionSeedBatchCommandHandler.LoadPdfBytesAsync (R2/blob-only; #2671).
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

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
