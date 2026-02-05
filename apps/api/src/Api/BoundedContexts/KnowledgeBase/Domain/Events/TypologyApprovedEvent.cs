using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an agent typology is approved.
/// Used for notification to the editor who created it.
/// Issue #3381: Typology Approval Workflow Endpoint.
/// </summary>
internal sealed class TypologyApprovedEvent : DomainEventBase
{
    public Guid TypologyId { get; }
    public string TypologyName { get; }
    public Guid CreatedBy { get; }
    public Guid ApprovedBy { get; }
    public string? Notes { get; }

    public TypologyApprovedEvent(
        Guid typologyId,
        string typologyName,
        Guid createdBy,
        Guid approvedBy,
        string? notes = null)
    {
        TypologyId = typologyId;
        TypologyName = typologyName;
        CreatedBy = createdBy;
        ApprovedBy = approvedBy;
        Notes = notes;
    }
}
