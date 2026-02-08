using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a rule is deactivated.
/// Issue #3759: Rules Arbitration Engine
/// </summary>
internal sealed class RuleDeactivatedEvent : DomainEventBase
{
    public Guid RuleId { get; }
    public Guid GameId { get; }

    public RuleDeactivatedEvent(Guid ruleId, Guid gameId)
    {
        RuleId = ruleId;
        GameId = gameId;
    }
}
