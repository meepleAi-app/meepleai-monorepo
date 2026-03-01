using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a processing job begins execution.
/// Issue #4730: Processing queue management.
/// </summary>
internal sealed class JobStartedEvent : DomainEventBase
{
    public Guid JobId { get; }
    public Guid PdfDocumentId { get; }

    public JobStartedEvent(Guid jobId, Guid pdfDocumentId)
    {
        JobId = jobId;
        PdfDocumentId = pdfDocumentId;
    }
}
