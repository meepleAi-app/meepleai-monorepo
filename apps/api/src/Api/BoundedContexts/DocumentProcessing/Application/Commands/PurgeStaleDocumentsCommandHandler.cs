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

        var staleDocs = await _dbContext.PdfDocuments
            .Where(p => activeStates.Contains(p.ProcessingState))
            .Where(p => p.UploadedAt < threshold)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var doc in staleDocs)
        {
            var originalState = doc.ProcessingState;
            doc.ProcessingState = nameof(PdfProcessingState.Failed);
            doc.ProcessingError = "Processing timed out (stale) - purged by admin";
            doc.ErrorCategory = "Service";
            doc.FailedAtState = originalState;
            doc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        if (staleDocs.Count > 0)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Maintenance batch completed: {Count} items processed",
                    staleDocs.Count);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(PurgeStaleDocumentsCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.C);
                _logger.LogDebug(ex,
                    "Concurrency conflict in {Handler} (Category C) — some items mutated concurrently by admin; batch partially applied",
                    nameof(PurgeStaleDocumentsCommandHandler));
                // No re-throw. Maintenance job is best-effort. Caller treats this as success.
            }
        }

        return new PurgeStaleResult(staleDocs.Count);
    }
}
