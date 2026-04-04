using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;

/// <summary>
/// Handles GetAllRuleConflictFaqsForGameQuery with pagination.
/// Issue #3966: CQRS query handlers for conflict FAQ retrieval.
/// </summary>
internal sealed class GetAllRuleConflictFaqsForGameQueryHandler
    : IQueryHandler<GetAllRuleConflictFaqsForGameQuery, PagedResult<RuleConflictFaqDto>>
{
    private readonly IRuleConflictFaqRepository _faqRepository;

    public GetAllRuleConflictFaqsForGameQueryHandler(IRuleConflictFaqRepository faqRepository)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
    }

    public async Task<PagedResult<RuleConflictFaqDto>> Handle(
        GetAllRuleConflictFaqsForGameQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var allFaqs = await _faqRepository.GetByGameIdAsync(
            query.GameId,
            cancellationToken)
            .ConfigureAwait(false);

        // Apply pagination in memory (repository returns ordered list)
        var totalCount = allFaqs.Count;
        var skip = (query.Page - 1) * query.PageSize;
        var pagedFaqs = allFaqs.Skip(skip).Take(query.PageSize).ToList();

        var dtos = pagedFaqs.Select(MapToDto).ToList();

        return new PagedResult<RuleConflictFaqDto>(
            dtos,
            totalCount,
            query.Page,
            query.PageSize);
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
