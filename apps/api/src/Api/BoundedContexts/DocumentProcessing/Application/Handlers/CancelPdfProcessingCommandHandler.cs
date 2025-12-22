using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handler for CancelPdfProcessingCommand.
/// Cancels background PDF processing with authorization checks.
/// </summary>
internal class CancelPdfProcessingCommandHandler : ICommandHandler<CancelPdfProcessingCommand, CancelProcessingResult>
{
    private readonly IMediator _mediator;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly ILogger<CancelPdfProcessingCommandHandler> _logger;

    public CancelPdfProcessingCommandHandler(
        IMediator mediator,
        IBackgroundTaskService backgroundTaskService,
        ILogger<CancelPdfProcessingCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CancelProcessingResult> Handle(
        CancelPdfProcessingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Processing CancelPdfProcessingCommand: PdfId={PdfId}, UserId={UserId}",
            command.PdfId, command.UserId);

        // Step 1: Check PDF ownership and status
        var pdf = await _mediator.Send(new GetPdfOwnershipQuery(command.PdfId), cancellationToken)
            .ConfigureAwait(false);

        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found for cancellation", command.PdfId);
            return new CancelProcessingResult(
                Success: false,
                Message: "PDF not found",
                ErrorCode: "NOT_FOUND"
            );
        }

        // Step 2: Authorization check
        if (pdf.UploadedByUserId != command.UserId && !command.IsAdmin)
        {
            _logger.LogWarning(
                "User {UserId} denied access to cancel PDF {PdfId} processing (owner: {OwnerId})",
                command.UserId, command.PdfId, pdf.UploadedByUserId);
            return new CancelProcessingResult(
                Success: false,
                Message: "Access denied",
                ErrorCode: "FORBIDDEN"
            );
        }

        // Step 3: Check if processing is still in progress
        if (string.Equals(pdf.ProcessingStatus, "completed", StringComparison.Ordinal) ||
            string.Equals(pdf.ProcessingStatus, "failed", StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "PDF {PdfId} processing already completed/failed (status: {Status})",
                command.PdfId, pdf.ProcessingStatus);
            return new CancelProcessingResult(
                Success: false,
                Message: "Processing already completed or failed",
                ErrorCode: "ALREADY_COMPLETED"
            );
        }

        // Step 4: Cancel background task (infrastructure service)
        var cancelled = _backgroundTaskService.CancelTask(command.PdfId.ToString());

        if (!cancelled)
        {
            _logger.LogWarning(
                "Failed to cancel processing for PDF {PdfId} - task not found",
                command.PdfId);
            return new CancelProcessingResult(
                Success: false,
                Message: "Processing task not found or already completed",
                ErrorCode: "TASK_NOT_FOUND"
            );
        }

        _logger.LogInformation(
            "User {UserId} successfully cancelled processing for PDF {PdfId}",
            command.UserId, command.PdfId);

        return new CancelProcessingResult(
            Success: true,
            Message: "Processing cancellation requested"
        );
    }
}
