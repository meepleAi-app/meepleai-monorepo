using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Raised when a job's priority is changed (drag-and-drop reorder).
/// Issue #4730: Processing queue management.
/// </summary>
internal sealed class JobPriorityChangedEvent : DomainEventBase
{
    public Guid JobId { get; }
    public int OldPriority { get; }
    public int NewPriority { get; }

    public JobPriorityChangedEvent(Guid jobId, int oldPriority, int newPriority)
    {
        JobId = jobId;
        OldPriority = oldPriority;
        NewPriority = newPriority;
    }
}
