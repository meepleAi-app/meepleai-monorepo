using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for RetryPdfProcessingCommand.
/// Issue #4216: Manual retry mechanism for failed PDF processing.
/// Simplified approach: Work directly with entity, apply domain logic inline.
/// </summary>
internal sealed class RetryPdfProcessingCommandHandler
    : ICommandHandler<RetryPdfProcessingCommand, RetryPdfProcessingResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<RetryPdfProcessingCommandHandler> _logger;

    public RetryPdfProcessingCommandHandler(
        MeepleAiDbContext db,
        IMediator mediator,
        ILogger<RetryPdfProcessingCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RetryPdfProcessingResult> Handle(
        RetryPdfProcessingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Load PDF document
        var pdf = await _db.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == command.PdfId, cancellationToken)
            .ConfigureAwait(false);

        if (pdf == null)
        {
            return new RetryPdfProcessingResult(
                Success: false,
                CurrentState: "NotFound",
                RetryCount: 0,
                Message: $"PDF document {command.PdfId} not found"
            );
        }

        // Authorization: Only owner can retry (admin check would go here if needed)
        if (pdf.UploadedByUserId != command.UserId)
        {
            return new RetryPdfProcessingResult(
                Success: false,
                CurrentState: pdf.ProcessingState,
                RetryCount: pdf.RetryCount,
                Message: $"User {command.UserId} is not authorized to retry this PDF"
            );
        }

        // Apply domain business logic inline
        const int MaxRetries = 3;
        var currentState = Enum.Parse<PdfProcessingState>(pdf.ProcessingState);

        // Check if retry is allowed
        if (pdf.RetryCount >= MaxRetries)
        {
            _logger.LogWarning(
                "Retry not allowed for PDF {PdfId}: Maximum retries ({MaxRetries}) reached",
                command.PdfId,
                MaxRetries);

            return new RetryPdfProcessingResult(
                Success: false,
                CurrentState: pdf.ProcessingState,
                RetryCount: pdf.RetryCount,
                Message: $"Maximum retry limit ({MaxRetries}) reached"
            );
        }

        if (currentState != PdfProcessingState.Failed)
        {
            _logger.LogWarning(
                "Retry not allowed for PDF {PdfId}: Current state is {State}, not Failed",
                command.PdfId,
                currentState);

            return new RetryPdfProcessingResult(
                Success: false,
                CurrentState: pdf.ProcessingState,
                RetryCount: pdf.RetryCount,
                Message: $"Cannot retry: document is in {currentState} state, not Failed"
            );
        }

        // Increment retry count
        pdf.RetryCount++;

        // Resume from failed state or restart from Extracting
        var resumeState = pdf.FailedAtState != null
            ? Enum.Parse<PdfProcessingState>(pdf.FailedAtState)
            : PdfProcessingState.Extracting;

        pdf.ProcessingState = resumeState.ToString();

        // Sync backward compatibility field
        pdf.ProcessingStatus = resumeState switch
        {
            PdfProcessingState.Pending => "pending",
            PdfProcessingState.Uploading or PdfProcessingState.Extracting or
            PdfProcessingState.Chunking or PdfProcessingState.Embedding or
            PdfProcessingState.Indexing => "processing",
            _ => "pending"
        };

        // Clear error state
        pdf.ProcessingError = null;
        pdf.ProcessedAt = null;

        // Save changes
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish domain event
        var retryEvent = new PdfRetryInitiatedEvent(
            pdf.Id,
            pdf.RetryCount,
            pdf.UploadedByUserId
        );
        await _mediator.Publish(retryEvent, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "PDF {PdfId} retry initiated: RetryCount={RetryCount}, ResumeState={State}",
            command.PdfId,
            pdf.RetryCount,
            resumeState);

        return new RetryPdfProcessingResult(
            Success: true,
            CurrentState: pdf.ProcessingState,
            RetryCount: pdf.RetryCount,
            Message: $"Retry {pdf.RetryCount} initiated, resuming from {resumeState}"
        );
    }
}
