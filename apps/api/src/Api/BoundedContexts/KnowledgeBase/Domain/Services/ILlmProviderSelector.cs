using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;
using Api.Services.LlmClients;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Issue #5487: Encapsulates LLM provider selection logic extracted from HybridLlmService.
/// Combines routing strategy, circuit breaker checks, emergency overrides, RPD quota checks,
/// and fallback chain traversal into a single selection decision.
/// </summary>
internal interface ILlmProviderSelector
{
    /// <summary>
    /// Select the best available provider and client for the given request context.
    /// Handles: routing strategy → emergency override → RPD quota check → circuit breaker → fallback chain.
    /// </summary>
    /// <returns>Selected client and routing decision, or null client if no provider available.</returns>
    Task<ProviderSelectionResult> SelectProviderAsync(
        User? user,
        RagStrategy strategy,
        RequestSource source,
        CancellationToken ct = default);

    /// <summary>
    /// Get the next fallback provider after a failure, excluding already-attempted providers.
    /// </summary>
    Task<ProviderSelectionResult> GetNextFallbackAsync(
        string failedProvider,
        HashSet<string> attemptedProviders,
        CancellationToken ct = default);
}

/// <summary>
/// Result of provider selection containing the client to use and the routing decision.
/// </summary>
internal sealed record ProviderSelectionResult(
    ILlmClient? Client,
    LlmRoutingDecision Decision)
{
    /// <summary>
    /// Whether a usable provider was found.
    /// </summary>
    public bool HasProvider => Client != null;

    /// <summary>
    /// Create a result indicating no provider is available.
    /// </summary>
    public static ProviderSelectionResult NoProvider(string reason) =>
        new(null, new LlmRoutingDecision("None", string.Empty, reason));
}
