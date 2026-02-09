using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a conflict FAQ is updated.
/// Issue #3761: Tracks FAQ modifications.
/// </summary>
internal sealed class RuleConflictFAQUpdatedEvent : DomainEventBase
{
    public Guid FAQId { get; }
    public Guid GameId { get; }
    public string Pattern { get; }

    public RuleConflictFAQUpdatedEvent(Guid faqId, Guid gameId, string pattern)
    {
        FAQId = faqId;
        GameId = gameId;
        Pattern = pattern;
    }
}
