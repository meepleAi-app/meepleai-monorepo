using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a rule is updated.
/// Issue #3759: Rules Arbitration Engine
/// </summary>
internal sealed class RuleUpdatedEvent : DomainEventBase
{
    public Guid RuleId { get; }
    public Guid GameId { get; }
    public string RuleName { get; }

    public RuleUpdatedEvent(Guid ruleId, Guid gameId, string ruleName)
    {
        RuleId = ruleId;
        GameId = gameId;
        RuleName = ruleName;
    }
}
