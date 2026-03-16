using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a rule dispute is resolved (arbitro or FAQ applied).
/// </summary>
internal sealed class DisputeResolvedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public RuleDisputeEntry Dispute { get; }

    public DisputeResolvedEvent(Guid sessionId, RuleDisputeEntry dispute)
    {
        SessionId = sessionId;
        Dispute = dispute;
    }
}
