using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a processing job is cancelled by an administrator.
/// Issue #4730: Processing queue management.
/// </summary>
internal sealed class JobCancelledEvent : DomainEventBase
{
    public Guid JobId { get; }
    public Guid PdfDocumentId { get; }
    public Guid UserId { get; }

    public JobCancelledEvent(Guid jobId, Guid pdfDocumentId, Guid userId)
    {
        JobId = jobId;
        PdfDocumentId = pdfDocumentId;
        UserId = userId;
    }
}
