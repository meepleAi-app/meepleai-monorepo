using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a failed job is scheduled for retry.
/// Issue #4730: Processing queue management.
/// </summary>
internal sealed class JobRetriedEvent : DomainEventBase
{
    public Guid JobId { get; }
    public Guid PdfDocumentId { get; }
    public Guid UserId { get; }
    public int RetryCount { get; }

    public JobRetriedEvent(Guid jobId, Guid pdfDocumentId, Guid userId, int retryCount)
    {
        JobId = jobId;
        PdfDocumentId = pdfDocumentId;
        UserId = userId;
        RetryCount = retryCount;
    }
}
