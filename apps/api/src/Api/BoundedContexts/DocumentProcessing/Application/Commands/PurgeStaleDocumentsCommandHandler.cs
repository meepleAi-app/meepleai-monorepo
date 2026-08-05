using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for PurgeStaleDocumentsCommand.
/// Marks documents stuck in active processing states (>24h) as failed.
/// PDF Storage Management Hub: Phase 5.
/// </summary>
internal sealed class PurgeStaleDocumentsCommandHandler
    : ICommandHandler<PurgeStaleDocumentsCommand, PurgeStaleResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PurgeStaleDocumentsCommandHandler> _logger;

    public PurgeStaleDocumentsCommandHandler(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<PurgeStaleDocumentsCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PurgeStaleResult> Handle(
        PurgeStaleDocumentsCommand command, CancellationToken cancellationToken)
    {
        var threshold = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-24);

        // Active states (non-terminal, excluding Pending)
        var activeStates = new[] { "Uploading", "Extracting", "Chunking", "Embedding", "Indexing" };

        // Issue #3572: select the candidates untracked, then load-mutate-save ONE document per
        // iteration. A single SaveChanges for the whole batch runs in one implicit transaction, so a
        // concurrency conflict on any one row rolled back every other document — while the handler
        // returned the selected count and reported success for a batch that persisted nothing (the
        // same "the count lies" defect #3564 fixed, reached through the concurrency path instead).
        // Per-item saves contain the blast radius to the conflicting document and let the returned
        // count reflect what was actually written.
        var staleIds = await _dbContext.PdfDocuments
            .Where(p => activeStates.Contains(p.ProcessingState))
            .Where(p => p.UploadedAt < threshold)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var purgedCount = 0;

        foreach (var pdfId in staleIds)
        {
            try
            {
                // Issue #3564: .AsTracking() overrides the global QueryTrackingBehavior.NoTracking
                // default (PERF-06) — without it the mutations below never reach the ChangeTracker
                // and SaveChangesAsync is a silent no-op.
                var doc = await _dbContext.PdfDocuments
                    .AsTracking()
                    .FirstOrDefaultAsync(p => p.Id == pdfId, cancellationToken)
                    .ConfigureAwait(false);

                // Re-check under tracking: the document may have been deleted, or may have left the
                // active states on its own, between the candidate query and this iteration.
                if (doc is null || !activeStates.Contains(doc.ProcessingState, StringComparer.Ordinal))
                {
                    continue;
                }

                var originalState = doc.ProcessingState;
                doc.ProcessingState = nameof(PdfProcessingState.Failed);
                doc.ProcessingError = "Processing timed out (stale) - purged by admin";
                doc.ErrorCategory = "Service";
                doc.FailedAtState = originalState;
                doc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                purgedCount++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(PurgeStaleDocumentsCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.C);
                _logger.LogDebug(ex,
                    "Concurrency conflict in {Handler} (Category C) — PDF {PdfId} was mutated concurrently by admin; skipped, not counted as purged",
                    nameof(PurgeStaleDocumentsCommandHandler), pdfId);
                // No re-throw. Maintenance job is best-effort: the remaining documents still run,
                // and the skipped one is left out of the returned count.
            }
            finally
            {
                // Per-item tracker reset (#534): a failed iteration must not leave a mutated entity
                // in the ChangeTracker, or the NEXT SaveChangesAsync would try to flush it again.
                _dbContext.ChangeTracker.Clear();
            }
        }

        if (staleIds.Count > 0)
        {
            _logger.LogInformation(
                "Maintenance batch completed: {Purged}/{Selected} items purged",
                purgedCount, staleIds.Count);
        }

        return new PurgeStaleResult(purgedCount);
    }
}
