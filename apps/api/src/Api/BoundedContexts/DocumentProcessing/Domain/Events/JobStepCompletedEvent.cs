using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a single pipeline step completes within a processing job.
/// Issue #4730: Processing queue management.
/// </summary>
internal sealed class JobStepCompletedEvent : DomainEventBase
{
    public Guid JobId { get; }
    public Guid PdfDocumentId { get; }
    public ProcessingStepType StepType { get; }
    public TimeSpan Duration { get; }

    public JobStepCompletedEvent(Guid jobId, Guid pdfDocumentId, ProcessingStepType stepType, TimeSpan duration)
    {
        JobId = jobId;
        PdfDocumentId = pdfDocumentId;
        StepType = stepType;
        Duration = duration;
    }
}
