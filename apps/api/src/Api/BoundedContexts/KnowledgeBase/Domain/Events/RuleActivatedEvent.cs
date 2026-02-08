using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a rule is activated.
/// Issue #3759: Rules Arbitration Engine
/// </summary>
internal sealed class RuleActivatedEvent : DomainEventBase
{
    public Guid RuleId { get; }
    public Guid GameId { get; }

    public RuleActivatedEvent(Guid ruleId, Guid gameId)
    {
        RuleId = ruleId;
        GameId = gameId;
    }
}
