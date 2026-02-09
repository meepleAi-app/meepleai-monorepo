using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;

/// <summary>
/// Query to get FAQ by pattern match.
/// Issue #3966: CQRS queries for conflict FAQ management.
/// </summary>
internal record GetRuleConflictFaqByPatternQuery(
    Guid GameId,
    string Pattern
) : IQuery<RuleConflictFaqDto?>;
