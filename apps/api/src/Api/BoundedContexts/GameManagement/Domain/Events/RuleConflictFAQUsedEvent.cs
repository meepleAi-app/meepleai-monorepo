using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a conflict FAQ resolution is applied.
/// Issue #3761: Tracks FAQ usage for analytics.
/// </summary>
internal sealed class RuleConflictFAQUsedEvent : DomainEventBase
{
    public Guid FAQId { get; }
    public Guid GameId { get; }
    public int TotalUsageCount { get; }

    public RuleConflictFAQUsedEvent(Guid faqId, Guid gameId, int totalUsageCount)
    {
        FAQId = faqId;
        GameId = gameId;
        TotalUsageCount = totalUsageCount;
    }
}
