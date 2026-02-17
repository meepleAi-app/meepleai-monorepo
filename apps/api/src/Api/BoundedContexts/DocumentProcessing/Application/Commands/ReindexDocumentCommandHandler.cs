using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for ReindexDocumentCommand.
/// Deletes chunks, resets state to Pending for re-processing.
/// PDF Storage Management Hub: Phase 5.
/// </summary>
internal sealed class ReindexDocumentCommandHandler : ICommandHandler<ReindexDocumentCommand>
{
    private readonly MeepleAiDbContext _dbContext;

    public ReindexDocumentCommandHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task Handle(ReindexDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == command.PdfId, cancellationToken)
            .ConfigureAwait(false);

        if (pdf is null)
        {
            throw new KeyNotFoundException($"PDF document {command.PdfId} not found");
        }

        // Delete associated text chunks
        var chunks = await _dbContext.TextChunks
            .Where(tc => tc.PdfDocumentId == command.PdfId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunks.Count > 0)
        {
            _dbContext.TextChunks.RemoveRange(chunks);
        }

        // Reset processing state to Pending for re-processing
        pdf.ProcessingState = "Pending";
        pdf.ProcessingStatus = "pending";
        pdf.ProcessedAt = null;
        pdf.ProcessingError = null;
        pdf.RetryCount = 0;
        pdf.ErrorCategory = null;
        pdf.FailedAtState = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
