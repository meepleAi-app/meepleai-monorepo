using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// DTO for RuleConflictFAQ aggregate.
/// Issue #3966: CQRS + API for RuleConflictFAQ management.
/// </summary>
public record RuleConflictFaqDto(
    Guid Id,
    Guid GameId,
    ConflictType ConflictType,
    string Pattern,
    string Resolution,
    int Priority,
    int UsageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// DTO for creating a new RuleConflictFAQ entry.
/// </summary>
public record CreateRuleConflictFaqDto(
    ConflictType ConflictType,
    string Pattern,
    string Resolution,
    int Priority
);

/// <summary>
/// DTO for updating FAQ resolution text.
/// </summary>
public record UpdateRuleConflictFaqResolutionDto(
    string Resolution
);
