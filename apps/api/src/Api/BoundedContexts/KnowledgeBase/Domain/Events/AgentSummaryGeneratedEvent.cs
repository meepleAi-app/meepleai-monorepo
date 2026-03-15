using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an agent generates a summary of the session (for resume context).
/// </summary>
internal sealed class AgentSummaryGeneratedEvent : DomainEventBase
{
    public Guid PauseSnapshotId { get; }
    public string Summary { get; }

    public AgentSummaryGeneratedEvent(Guid pauseSnapshotId, string summary)
    {
        PauseSnapshotId = pauseSnapshotId;
        Summary = summary;
    }
}
