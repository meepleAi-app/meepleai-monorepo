namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing agent readiness status for chat.
/// </summary>
public record AgentStatusDto(
    Guid AgentId,
    string Name,
    bool IsActive,
    bool IsReady,
    bool HasConfiguration,
    bool HasDocuments,
    int DocumentCount,
    string RagStatus,
    string? BlockingReason
);
