using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a new PDF processing job is added to the queue.
/// Issue #4730: Processing queue management.
/// </summary>
internal sealed class JobQueuedEvent : DomainEventBase
{
    public Guid JobId { get; }
    public Guid PdfDocumentId { get; }
    public Guid UserId { get; }
    public int Priority { get; }

    public JobQueuedEvent(Guid jobId, Guid pdfDocumentId, Guid userId, int priority)
    {
        JobId = jobId;
        PdfDocumentId = pdfDocumentId;
        UserId = userId;
        Priority = priority;
    }
}
