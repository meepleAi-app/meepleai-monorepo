using Api.BoundedContexts.Authentication.Domain.Entities;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Strategy for determining which LLM client and model to use for a given request
/// ISSUE-958: Hybrid LLM architecture with adaptive routing
/// </summary>
public interface ILlmRoutingStrategy
{
    /// <summary>
    /// Select the appropriate LLM provider and model based on routing logic
    /// </summary>
    /// <param name="user">User making the request (null for anonymous)</param>
    /// <param name="context">Additional context for routing decision</param>
    /// <returns>Routing decision with provider name and model ID</returns>
    LlmRoutingDecision SelectProvider(User? user, string? context = null);
}

/// <summary>
/// Result of LLM routing decision
/// </summary>
public record LlmRoutingDecision(
    string ProviderName,
    string ModelId,
    string Reason)
{
    /// <summary>
    /// Create decision for OpenRouter provider
    /// </summary>
    public static LlmRoutingDecision OpenRouter(string modelId, string reason) =>
        new("OpenRouter", modelId, reason);

    /// <summary>
    /// Create decision for Ollama provider
    /// </summary>
    public static LlmRoutingDecision Ollama(string modelId, string reason) =>
        new("Ollama", modelId, reason);
}
