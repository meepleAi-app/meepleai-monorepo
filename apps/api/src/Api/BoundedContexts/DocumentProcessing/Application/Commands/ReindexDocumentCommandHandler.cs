using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for ReindexDocumentCommand.
/// Deletes chunks, resets state to Pending, and enqueues for re-processing.
/// PDF Storage Management Hub: Phase 5.
/// </summary>
internal sealed class ReindexDocumentCommandHandler : ICommandHandler<ReindexDocumentCommand>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<ReindexDocumentCommandHandler> _logger;

    public ReindexDocumentCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        ILogger<ReindexDocumentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        pdf.ProcessedAt = null;
        pdf.ProcessingError = null;
        pdf.RetryCount = 0;
        pdf.ErrorCategory = null;
        pdf.FailedAtState = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Enqueue for Quartz-based processing (ensures the job is picked up)
        try
        {
            var userId = pdf.UploadedByUserId;
            await _mediator.Send(
                new EnqueuePdfCommand(command.PdfId, userId, Priority: 0),
                cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Reindexed PDF {PdfId} enqueued for processing", command.PdfId);
        }
#pragma warning disable CA1031 // Best-effort enqueue
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enqueue reindexed PDF {PdfId} (may already be queued)", command.PdfId);
        }
#pragma warning restore CA1031
    }
}
