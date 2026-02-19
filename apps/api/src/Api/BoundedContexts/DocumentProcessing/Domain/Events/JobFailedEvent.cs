using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a processing job fails.
/// Triggers user and admin notifications.
/// Issue #4730: Processing queue management.
/// </summary>
internal sealed class JobFailedEvent : DomainEventBase
{
    public Guid JobId { get; }
    public Guid PdfDocumentId { get; }
    public Guid UserId { get; }
    public string ErrorMessage { get; }
    public ProcessingStepType? FailedAtStep { get; }
    public int RetryCount { get; }

    public JobFailedEvent(
        Guid jobId,
        Guid pdfDocumentId,
        Guid userId,
        string errorMessage,
        ProcessingStepType? failedAtStep,
        int retryCount)
    {
        JobId = jobId;
        PdfDocumentId = pdfDocumentId;
        UserId = userId;
        ErrorMessage = errorMessage;
        FailedAtStep = failedAtStep;
        RetryCount = retryCount;
    }
}
