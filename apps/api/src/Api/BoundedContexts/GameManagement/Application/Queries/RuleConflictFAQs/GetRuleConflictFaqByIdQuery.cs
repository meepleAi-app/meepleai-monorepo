using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;

/// <summary>
/// Query to get FAQ by ID.
/// Issue #3966: CQRS queries for conflict FAQ management.
/// </summary>
internal record GetRuleConflictFaqByIdQuery(
    Guid Id
) : IQuery<RuleConflictFaqDto?>;
