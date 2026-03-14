using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Strategy for determining which LLM client and model to use for a given request
/// ISSUE-958: Hybrid LLM architecture with adaptive routing
/// </summary>
internal interface ILlmRoutingStrategy
{
    /// <summary>
    /// Select the appropriate LLM provider and model based on routing logic.
    /// Issue #3435: Strategy-based routing (strategy determines model, tier validates access).
    /// Issue #28: Region-aware routing — region parameter accepted but currently ignored (no-op).
    /// </summary>
    /// <param name="user">User making the request (null for anonymous)</param>
    /// <param name="strategy">RAG strategy that determines model selection</param>
    /// <param name="context">Additional context for routing decision</param>
    /// <param name="region">Geographic region hint for future multi-region routing (currently ignored)</param>
    /// <returns>Routing decision with provider name and model ID</returns>
    LlmRoutingDecision SelectProvider(User? user, RagStrategy strategy, string? context = null, string? region = null);
}

/// <summary>
/// Result of LLM routing decision
/// </summary>
internal record LlmRoutingDecision(
    string ProviderName,
    string ModelId,
    string Reason)
{
    /// <summary>
    /// Geographic region hint for future multi-region routing.
    /// Currently always null — populated when region detection is implemented.
    /// Candidates: GeoIP middleware, user profile, CDN edge header (CF-IPCountry).
    /// Issue #107: Epic G1 — Multi-Region Preparation.
    /// </summary>
    public string? UserRegion { get; init; }

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
