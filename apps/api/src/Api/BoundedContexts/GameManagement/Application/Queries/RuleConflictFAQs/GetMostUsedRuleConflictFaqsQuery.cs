using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;

/// <summary>
/// Query to get most frequently used FAQs for analytics.
/// Issue #3966: CQRS queries for conflict FAQ management.
/// </summary>
internal record GetMostUsedRuleConflictFaqsQuery(
    Guid GameId,
    int Limit = 10
) : IQuery<List<RuleConflictFaqDto>>;
