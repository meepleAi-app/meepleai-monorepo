using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a processing job completes all pipeline steps successfully.
/// Triggers user notifications (in-app, email, push).
/// Issue #4730: Processing queue management.
/// </summary>
internal sealed class JobCompletedEvent : DomainEventBase
{
    public Guid JobId { get; }
    public Guid PdfDocumentId { get; }
    public Guid UserId { get; }
    public TimeSpan TotalDuration { get; }

    public JobCompletedEvent(Guid jobId, Guid pdfDocumentId, Guid userId, TimeSpan totalDuration)
    {
        JobId = jobId;
        PdfDocumentId = pdfDocumentId;
        UserId = userId;
        TotalDuration = totalDuration;
    }
}
