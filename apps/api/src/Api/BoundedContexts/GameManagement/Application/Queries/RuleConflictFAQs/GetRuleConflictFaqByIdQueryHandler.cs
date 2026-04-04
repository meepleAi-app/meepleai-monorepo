using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;

/// <summary>
/// Handles GetRuleConflictFaqByIdQuery.
/// Issue #3966: CQRS query handlers for conflict FAQ retrieval.
/// </summary>
internal sealed class GetRuleConflictFaqByIdQueryHandler
    : IQueryHandler<GetRuleConflictFaqByIdQuery, RuleConflictFaqDto?>
{
    private readonly IRuleConflictFaqRepository _faqRepository;

    public GetRuleConflictFaqByIdQueryHandler(IRuleConflictFaqRepository faqRepository)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
    }

    public async Task<RuleConflictFaqDto?> Handle(
        GetRuleConflictFaqByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var faq = await _faqRepository.GetByIdAsync(
            query.Id,
            cancellationToken)
            .ConfigureAwait(false);

        return faq != null ? MapToDto(faq) : null;
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
