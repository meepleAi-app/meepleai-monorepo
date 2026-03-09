namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing the current LLM configuration of an agent.
/// </summary>
public record AgentConfigurationDto(
    Guid Id,
    Guid AgentId,
    string LlmModel,
    string LlmProvider,
    decimal Temperature,
    int MaxTokens,
    IReadOnlyList<Guid> SelectedDocumentIds,
    bool IsCurrent,
    DateTime CreatedAt);
