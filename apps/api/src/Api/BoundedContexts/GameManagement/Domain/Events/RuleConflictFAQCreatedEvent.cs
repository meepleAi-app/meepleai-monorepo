using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a rule conflict FAQ is created.
/// Issue #3761: Conflict Resolution FAQ System.
/// </summary>
internal sealed class RuleConflictFAQCreatedEvent : DomainEventBase
{
    public Guid FAQId { get; }
    public Guid GameId { get; }
    public string Pattern { get; }

    public RuleConflictFAQCreatedEvent(Guid faqId, Guid gameId, string pattern)
    {
        FAQId = faqId;
        GameId = gameId;
        Pattern = pattern;
    }
}
