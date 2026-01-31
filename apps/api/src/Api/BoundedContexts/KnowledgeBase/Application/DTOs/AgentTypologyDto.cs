namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Data Transfer Object for AgentTypology aggregate.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
internal record AgentTypologyDto(
    Guid Id,
    string Name,
    string Description,
    string BasePrompt,
    string DefaultStrategyName,
    IReadOnlyDictionary<string, object> DefaultStrategyParameters,
    string Status,
    Guid CreatedBy,
    Guid? ApprovedBy,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    bool IsDeleted
);
