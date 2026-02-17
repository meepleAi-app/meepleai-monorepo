using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

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

    public PurgeStaleDocumentsCommandHandler(MeepleAiDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<PurgeStaleResult> Handle(
        PurgeStaleDocumentsCommand command, CancellationToken cancellationToken)
    {
        var threshold = _timeProvider.GetUtcNow().DateTime.AddHours(-24);

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
            doc.ProcessingState = "Failed";
            doc.ProcessingStatus = "failed";
            doc.ProcessingError = "Processing timed out (stale) - purged by admin";
            doc.ErrorCategory = "Service";
            doc.FailedAtState = originalState;
            doc.ProcessedAt = _timeProvider.GetUtcNow().DateTime;
        }

        if (staleDocs.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new PurgeStaleResult(staleDocs.Count);
    }
}
