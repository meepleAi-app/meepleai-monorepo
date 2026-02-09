using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.QueryHandlers.RuleConflictFAQs;

/// <summary>
/// Handles GetMostUsedRuleConflictFaqsQuery for analytics.
/// Issue #3966: CQRS query handlers for conflict FAQ retrieval.
/// </summary>
internal sealed class GetMostUsedRuleConflictFaqsQueryHandler
    : IQueryHandler<GetMostUsedRuleConflictFaqsQuery, List<RuleConflictFaqDto>>
{
    private readonly IRuleConflictFaqRepository _faqRepository;

    public GetMostUsedRuleConflictFaqsQueryHandler(IRuleConflictFaqRepository faqRepository)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
    }

    public async Task<List<RuleConflictFaqDto>> Handle(
        GetMostUsedRuleConflictFaqsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var allFaqs = await _faqRepository.GetByGameIdAsync(
            query.GameId,
            cancellationToken)
            .ConfigureAwait(false);

        // Repository already orders by UsageCount DESC, just take top N
        var topFaqs = allFaqs.Take(query.Limit).ToList();

        return topFaqs.Select(MapToDto).ToList();
    }

    private static RuleConflictFaqDto MapToDto(Domain.Entities.RuleConflictFAQ faq)
    {
        return new RuleConflictFaqDto(
            faq.Id,
            faq.GameId,
            faq.ConflictType,
            faq.Pattern,
            faq.Resolution,
            faq.Priority,
            faq.UsageCount,
            faq.CreatedAt,
            faq.UpdatedAt);
    }
}
