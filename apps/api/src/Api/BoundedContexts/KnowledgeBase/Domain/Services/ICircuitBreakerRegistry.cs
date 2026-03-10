using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Issue #5487: Shared circuit breaker and latency tracking registry.
/// Extracted from HybridLlmService to allow both the provider selector
/// (which checks state) and the orchestrator (which records outcomes) to
/// access the same circuit breaker state.
/// </summary>
internal interface ICircuitBreakerRegistry
{
    /// <summary>
    /// Initialize circuit breaker and latency tracking for a provider.
    /// </summary>
    void Initialize(string providerName);

    /// <summary>
    /// Check if a provider's circuit breaker allows requests.
    /// </summary>
    bool AllowsRequests(string providerName);

    /// <summary>
    /// Get the current circuit state enum for a provider.
    /// </summary>
    CircuitState GetState(string providerName);

    /// <summary>
    /// Record a successful request for circuit breaker and latency tracking.
    /// </summary>
    void RecordSuccess(string providerName, long latencyMs);

    /// <summary>
    /// Record a failed request for circuit breaker and latency tracking.
    /// </summary>
    void RecordFailure(string providerName, long latencyMs);

    /// <summary>
    /// Reset circuit breaker state for a specific provider (or all if null).
    /// </summary>
    void ResetCircuitBreaker(string? targetProvider = null);

    /// <summary>
    /// Get human-readable circuit state string for a provider.
    /// </summary>
    string GetCircuitStateDescription(string providerName);

    /// <summary>
    /// Get latency statistics summary for a provider.
    /// </summary>
    string GetLatencyStats(string providerName);

    /// <summary>
    /// Get monitoring status for all tracked providers.
    /// </summary>
    Dictionary<string, (string circuitState, string latencyStats)> GetMonitoringStatus();
}
