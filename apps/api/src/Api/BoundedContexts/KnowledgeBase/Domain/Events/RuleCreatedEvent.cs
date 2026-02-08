using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a new rule is created.
/// Issue #3759: Rules Arbitration Engine
/// </summary>
internal sealed class RuleCreatedEvent : DomainEventBase
{
    public Guid RuleId { get; }
    public Guid GameId { get; }
    public string RuleName { get; }
    public string RuleType { get; }

    public RuleCreatedEvent(Guid ruleId, Guid gameId, string ruleName, string ruleType)
    {
        RuleId = ruleId;
        GameId = gameId;
        RuleName = ruleName;
        RuleType = ruleType;
    }
}
