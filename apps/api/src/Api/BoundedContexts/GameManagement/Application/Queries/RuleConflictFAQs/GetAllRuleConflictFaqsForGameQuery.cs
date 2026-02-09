using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;

/// <summary>
/// Query to get all FAQs for a game with pagination.
/// Issue #3966: CQRS queries for conflict FAQ management.
/// </summary>
internal record GetAllRuleConflictFaqsForGameQuery(
    Guid GameId,
    int Page = 1,
    int PageSize = 20
) : IQuery<PagedResult<RuleConflictFaqDto>>;
