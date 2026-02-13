using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to retry processing of a failed PDF document.
/// Issue #4216: Manual retry mechanism with error categorization.
/// </summary>
/// <param name="PdfId">The PDF document ID to retry processing for</param>
/// <param name="UserId">The requesting user ID (for authorization)</param>
internal record RetryPdfProcessingCommand(
    Guid PdfId,
    Guid UserId
) : ICommand<RetryPdfProcessingResult>;

/// <summary>
/// Result of a PDF retry operation.
/// </summary>
/// <param name="Success">Whether the retry was initiated successfully</param>
/// <param name="CurrentState">The current processing state after retry</param>
/// <param name="RetryCount">The updated retry count</param>
/// <param name="Message">Optional message (error or success description)</param>
public record RetryPdfProcessingResult(
    bool Success,
    string CurrentState,
    int RetryCount,
    string? Message = null
);
