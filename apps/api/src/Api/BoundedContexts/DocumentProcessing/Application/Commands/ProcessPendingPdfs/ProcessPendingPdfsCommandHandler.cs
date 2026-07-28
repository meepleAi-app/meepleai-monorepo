using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.ProcessPendingPdfs;

/// <summary>
/// Handler for ProcessPendingPdfsCommand — the admin "unstick the pipeline" recovery endpoint.
/// Selects Pending PDFs (plus in-flight PDFs that are STALE per a 30-minute cutoff) and routes each
/// through the shared pipeline's ATOMIC Pending-only claim
/// (<see cref="IPdfClaimService.TryClaimPendingAsync"/>), so a document already being processed by a
/// concurrent worker or sweep is skipped instead of being re-extracted and re-indexed in parallel.
///
/// <para>The old handler selected every non-terminal PDF and ran a blind Extract + Index with no
/// claim, so two concurrent admin sweeps (or a sweep racing the Quartz pipeline) both processed the
/// SAME in-flight document — duplicating/tearing its text_chunks + pgvector rows (the child tables
/// have no unique (PdfDocumentId, ChunkIndex) and xmin only guards pdf_documents). Mirrors
/// StalePdfRecoveryService. Bug-hunt B14 (#3269).</para>
/// </summary>
internal sealed class ProcessPendingPdfsCommandHandler : ICommandHandler<ProcessPendingPdfsCommand, ProcessPendingPdfsResult>
{
    /// <summary>An in-flight (non-Pending) PDF older than this is considered stale and recoverable.</summary>
    private static readonly TimeSpan ProcessingStaleness = TimeSpan.FromMinutes(30);

    private readonly MeepleAiDbContext _dbContext;
    private readonly IPdfProcessingPipelineService _pipeline;
    private readonly ILogger<ProcessPendingPdfsCommandHandler> _logger;

    public ProcessPendingPdfsCommandHandler(
        MeepleAiDbContext dbContext,
        IPdfProcessingPipelineService pipeline,
        ILogger<ProcessPendingPdfsCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessPendingPdfsResult> Handle(
        ProcessPendingPdfsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pendingState = nameof(PdfProcessingState.Pending);
        var uploadingState = nameof(PdfProcessingState.Uploading);
        var extractingState = nameof(PdfProcessingState.Extracting);
        var chunkingState = nameof(PdfProcessingState.Chunking);
        var embeddingState = nameof(PdfProcessingState.Embedding);
        var indexingState = nameof(PdfProcessingState.Indexing);
        var processingCutoff = DateTime.UtcNow - ProcessingStaleness;

        // Recoverable = Pending (any age — this is an explicit admin trigger), OR an in-flight state
        // ONLY if stale, so we never clobber a document the normal pipeline is actively processing.
        // Demo mock placeholders (seed/ prefix, no real blob) are excluded: force-processing them
        // would only fail on the missing blob and flip them to Failed.
        var candidates = await _dbContext.PdfDocuments
            .Where(p => !p.FilePath.StartsWith(PdfDocumentEntity.DemoMockFilePathPrefix))
            .Where(p =>
                p.ProcessingState == pendingState ||
                ((p.ProcessingState == uploadingState ||
                  p.ProcessingState == extractingState ||
                  p.ProcessingState == chunkingState ||
                  p.ProcessingState == embeddingState ||
                  p.ProcessingState == indexingState) && p.UploadedAt < processingCutoff))
            .OrderByDescending(p => p.ProcessingPriority) // Admin > Normal
            .ThenBy(p => p.UploadedAt)
            .Select(p => new { p.Id, p.FileName, p.FilePath, p.UploadedByUserId, p.ProcessingState })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Found {Count} recoverable PDF(s) to process", candidates.Count);

        var triggered = 0;
        var failed = 0;
        var pdfIds = new List<string>();

        foreach (var pdf in candidates)
        {
            try
            {
                // Stale in-flight docs are reset to Pending first so the pipeline's atomic claim can
                // pick them up (ProcessAsync only claims Pending). Pending docs are processed as-is.
                if (!string.Equals(pdf.ProcessingState, pendingState, StringComparison.Ordinal))
                {
                    await ResetToPendingAsync(pdf.Id, cancellationToken).ConfigureAwait(false);
                }

                // ProcessAsync internally does TryClaimPendingAsync (atomic Pending → Extracting); a
                // document already claimed by a concurrent worker/sweep is skipped without
                // corruption — closing the race the old blind Extract + Index left open (B14).
                await _pipeline.ProcessAsync(pdf.Id, pdf.FilePath, pdf.UploadedByUserId, cancellationToken)
                    .ConfigureAwait(false);

                triggered++;
                pdfIds.Add(pdf.Id.ToString());
                _logger.LogInformation("Processed PDF {PdfId} ({FileName})", pdf.Id, pdf.FileName);
            }
#pragma warning disable CA1031 // Non-blocking: log and continue to the next PDF
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Failed to process PDF {PdfId} ({FileName})", pdf.Id, pdf.FileName);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation(
            "Batch PDF processing completed: {Triggered} triggered, {Failed} failed out of {Total} total",
            triggered, failed, candidates.Count);

        return new ProcessPendingPdfsResult(
            TotalPending: candidates.Count,
            Triggered: triggered,
            Failed: failed,
            PdfIds: pdfIds);
    }

    private async Task ResetToPendingAsync(Guid pdfDocumentId, CancellationToken cancellationToken)
    {
        var readyState = nameof(PdfProcessingState.Ready);
        // `.AsTracking()` is required because the production MeepleAiDbContext defaults to NoTracking
        // (PERF-06); without it the mutation is silently dropped.
        var pdf = await _dbContext.PdfDocuments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (pdf != null && !string.Equals(pdf.ProcessingState, readyState, StringComparison.Ordinal))
        {
            pdf.ProcessingState = nameof(PdfProcessingState.Pending);
            pdf.ProcessingError = null;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
