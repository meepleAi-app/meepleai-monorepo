namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Data Transfer Object for Agent aggregate.
/// </summary>
internal record AgentDto(
    Guid Id,
    string Name,
    string Type,
    string StrategyName,
    IReadOnlyDictionary<string, object> StrategyParameters,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastInvokedAt,
    int InvocationCount,
    bool IsRecentlyUsed,
    bool IsIdle
);
