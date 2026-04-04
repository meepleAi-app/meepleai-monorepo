using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.Services.LlmClients;
using RagStrategy = Api.BoundedContexts.KnowledgeBase.Domain.Enums.RagStrategy;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Issue #5487: Encapsulates LLM provider selection logic extracted from HybridLlmService.
/// Issue #5492: Also records request outcomes (circuit breaker + rate limit tracking).
/// </summary>
internal interface ILlmProviderSelector
{
    /// <summary>
    /// Select the best available provider and client for the given request context.
    /// Handles: routing strategy → emergency override → RPD quota check → circuit breaker → fallback chain.
    /// </summary>
    Task<ProviderSelectionResult> SelectProviderAsync(
        LlmUserContext userContext,
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

    /// <summary>
    /// Issue #5492: Record a successful request outcome (circuit breaker + rate limit tracking).
    /// </summary>
    void RecordSuccess(string providerName, string modelId, long latencyMs, LlmCompletionResult result);

    /// <summary>
    /// Issue #5492: Record a failed request outcome (circuit breaker + rate limit error tracking).
    /// </summary>
    void RecordFailure(string providerName, string modelId, long latencyMs, LlmCompletionResult? result = null);

    /// <summary>
    /// Issue #5492: Warn if approaching RPM limit before making a request.
    /// </summary>
    Task WarnIfApproachingLimitAsync(string providerName, CancellationToken ct = default);
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
