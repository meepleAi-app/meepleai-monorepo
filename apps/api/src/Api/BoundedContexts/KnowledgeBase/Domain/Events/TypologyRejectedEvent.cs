using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an agent typology is rejected.
/// Used for notification to the editor who created it.
/// Issue #3381: Typology Approval Workflow Endpoint.
/// </summary>
internal sealed class TypologyRejectedEvent : DomainEventBase
{
    public Guid TypologyId { get; }
    public string TypologyName { get; }
    public Guid CreatedBy { get; }
    public Guid RejectedBy { get; }
    public string Reason { get; }

    public TypologyRejectedEvent(
        Guid typologyId,
        string typologyName,
        Guid createdBy,
        Guid rejectedBy,
        string reason)
    {
        TypologyId = typologyId;
        TypologyName = typologyName;
        CreatedBy = createdBy;
        RejectedBy = rejectedBy;
        Reason = reason;
    }
}
