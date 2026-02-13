using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for RetryPdfProcessingCommand.
/// Issue #4216: Manual retry mechanism for failed PDF processing.
/// Refactored to use domain method for proper DDD architecture.
/// </summary>
internal sealed class RetryPdfProcessingCommandHandler
    : ICommandHandler<RetryPdfProcessingCommand, RetryPdfProcessingResult>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<RetryPdfProcessingCommandHandler> _logger;

    public RetryPdfProcessingCommandHandler(
        IPdfDocumentRepository pdfRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<RetryPdfProcessingCommandHandler> logger)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RetryPdfProcessingResult> Handle(
        RetryPdfProcessingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Load PDF document using repository (returns domain model)
        var pdf = await _pdfRepository.GetByIdAsync(command.PdfId, cancellationToken)
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
                CurrentState: pdf.ProcessingState.ToString(),
                RetryCount: pdf.RetryCount,
                Message: $"User {command.UserId} is not authorized to retry this PDF"
            );
        }

        // Apply domain retry logic with proper validation and state transitions
        try
        {
            pdf.Retry();

            // Update via repository (handles mapping and persistence)
            await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Publish domain event
            var retryEvent = new PdfRetryInitiatedEvent(
                pdf.Id,
                pdf.RetryCount,
                pdf.UploadedByUserId
            );
            await _mediator.Publish(retryEvent, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "PDF {PdfId} retry initiated: RetryCount={RetryCount}, State={State}",
                command.PdfId,
                pdf.RetryCount,
                pdf.ProcessingState);

            return new RetryPdfProcessingResult(
                Success: true,
                CurrentState: pdf.ProcessingState.ToString(),
                RetryCount: pdf.RetryCount,
                Message: $"Retry {pdf.RetryCount} initiated"
            );
        }
        catch (InvalidOperationException ex)
        {
            // Domain validation failed (max retries reached, wrong state, etc.)
            _logger.LogWarning(
                ex,
                "Retry not allowed for PDF {PdfId}: {Reason}",
                command.PdfId,
                ex.Message);

            return new RetryPdfProcessingResult(
                Success: false,
                CurrentState: pdf.ProcessingState.ToString(),
                RetryCount: pdf.RetryCount,
                Message: ex.Message
            );
        }
    }
}
