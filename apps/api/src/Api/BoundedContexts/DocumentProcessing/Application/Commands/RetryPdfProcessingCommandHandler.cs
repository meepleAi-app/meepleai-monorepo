using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for RetryPdfProcessingCommand.
/// Issue #4216: Manual retry mechanism for failed PDF processing.
/// Issue #5189: Added IsAdmin flag; aligned error handling to throw NotFoundException/ForbiddenException.
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
            throw new NotFoundException("PdfDocument", command.PdfId.ToString());

        // Authorization: admin can retry any PDF; owner can retry their own
        if (!command.IsAdmin && pdf.UploadedByUserId != command.UserId)
        {
            throw new ForbiddenException(
                $"User {command.UserId} is not authorized to retry PDF {command.PdfId}");
        }

        // Apply domain retry logic with proper validation and state transitions
        try
        {
            pdf.Retry();

            // Update via repository (handles mapping and persistence)
            await _pdfRepository.UpdateAsync(pdf, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Enqueue the PDF so the pipeline actually reprocesses it. Retry() only resets the
            // document to Pending; without an enqueue nothing picks it up — both the manual retry
            // (this handler) and the automatic RetryFailedPdfsJob were dead loops before this fix
            // (bug-hunt B11, #3269). Best-effort: an active job may already exist (e.g. a concurrent
            // retry), in which case EnqueuePdfCommand throws ConflictException and the existing job
            // will reprocess the now-Pending document — mirror ReindexDocumentCommandHandler.
            try
            {
                await _mediator.Send(
                    new EnqueuePdfCommand(pdf.Id, pdf.UploadedByUserId, Priority: 0),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ConflictException ex)
            {
                _logger.LogInformation(
                    ex,
                    "PDF {PdfId} already has an active job; the retry will reprocess via that job",
                    command.PdfId);
            }

            // Publish domain event
            var retryEvent = new PdfRetryInitiatedEvent(
                pdf.Id,
                pdf.RetryCount,
                pdf.UploadedByUserId
            );
            await _mediator.Publish(retryEvent, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "PDF {PdfId} retry initiated by {UserId} (IsAdmin={IsAdmin}): RetryCount={RetryCount}, State={State}",
                command.PdfId,
                command.UserId,
                command.IsAdmin,
                pdf.RetryCount,
                pdf.ProcessingState);

            return new RetryPdfProcessingResult(
                Success: true,
                CurrentState: pdf.ProcessingState.ToString(),
                RetryCount: pdf.RetryCount,
                Message: $"Retry {pdf.RetryCount} initiated"
            );
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(RetryPdfProcessingCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.A);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category A)",
                command.PdfId, nameof(RetryPdfProcessingCommandHandler));
            throw new ConflictException(
                $"Document {command.PdfId} was modified by another concurrent operation; please retry.");
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
