namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing the current LLM configuration of an agent
/// (model/provider/temperature/maxTokens). Per-agent KB linking (selected documents) is NOT
/// part of this contract — it is owned by the <c>AgentConfiguration</c> aggregate (#2391) and
/// its <c>updateSelectedDocuments</c> endpoint (Issue #3394 removed the previously exposed,
/// always-empty <c>SelectedDocumentIds</c> field).
/// </summary>
public record AgentConfigurationDto(
    Guid Id,
    Guid AgentId,
    string LlmModel,
    string LlmProvider,
    decimal Temperature,
    int MaxTokens,
    bool IsCurrent,
    DateTime CreatedAt);
