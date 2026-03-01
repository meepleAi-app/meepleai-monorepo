namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Response DTO for the orchestrated agent creation flow.
/// Issue #4772: Agent Creation Orchestration Flow.
/// </summary>
internal record AgentCreationResultDto(
    Guid AgentId,
    string AgentName,
    Guid ThreadId,
    int SlotUsed,
    bool GameAddedToCollection
);
