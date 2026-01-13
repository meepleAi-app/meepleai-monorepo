namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// LLM provider types for agent configuration.
/// Issue #2391 Sprint 2
/// </summary>
public enum LlmProvider
{
    /// <summary>
    /// OpenRouter cloud-based LLM gateway.
    /// </summary>
    OpenRouter = 0,

    /// <summary>
    /// Ollama local LLM deployment.
    /// </summary>
    Ollama = 1
}
