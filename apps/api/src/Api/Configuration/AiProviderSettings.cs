namespace Api.Configuration;

/// <summary>
/// Configuration for AI provider selection and fallback (BGAI-021, Issue #963).
/// Option C: Coexists with LlmRouting for backward compatibility.
/// </summary>
/// <remarks>
/// Architecture:
/// - PreferredProvider: Optional global preference (empty = use user-tier routing)
/// - Providers: Per-provider configuration with enable/disable toggles
/// - FallbackChain: Provider fallback order for circuit breaker
/// - CircuitBreaker: Shared settings for all providers
///
/// Integration with Existing System (Option C):
/// 1. Check AI.Providers[provider].Enabled before using provider
/// 2. Use PreferredProvider if set (overrides user-tier routing)
/// 3. Fall back to LlmRouting tier-based logic if PreferredProvider empty
/// 4. Use FallbackChain for circuit breaker failover order
/// </remarks>
public class AiProviderSettings
{
    public const string SectionName = "AI";

    /// <summary>
    /// Preferred provider for all users (empty = use user-tier routing from LlmRouting).
    /// Valid values: "Ollama", "OpenRouter", "" (empty)
    /// </summary>
    public string PreferredProvider { get; set; } = "";

    /// <summary>
    /// Per-provider configuration dictionary.
    /// </summary>
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Provider fallback order for circuit breaker (e.g., ["Ollama", "OpenRouter"]).
    /// Providers must exist in Providers dictionary and have Enabled = true.
    /// </summary>
    public List<string> FallbackChain { get; set; } = new();

    /// <summary>
    /// Circuit breaker configuration shared across all providers.
    /// </summary>
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Configuration for a single AI provider (Ollama, OpenRouter, etc.).
/// </summary>
public class ProviderConfig
{
    /// <summary>
    /// Whether this provider is enabled.
    /// If false, provider is skipped even if in FallbackChain.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Base URL for the provider API.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// List of models available from this provider.
    /// Used for validation and model selection.
    /// </summary>
    /// <remarks>
    /// Note: API keys (e.g., OpenRouter) are NOT configured here.
    /// They are read from environment variables using SecretsHelper (SEC-708):
    /// - OPENROUTER_API_KEY: Direct environment variable
    /// - OPENROUTER_API_KEY_FILE: Path to Docker secret file
    /// See: Infrastructure/SecretsHelper.cs for implementation.
    /// </remarks>
    public List<string> Models { get; set; } = new();

    /// <summary>
    /// Health check interval in seconds (default: 60s).
    /// Used by ProviderHealthCheckService for monitoring.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Circuit breaker configuration for provider failure handling.
/// </summary>
public class CircuitBreakerConfig
{
    /// <summary>
    /// Number of consecutive failures before opening circuit (default: 5).
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration in seconds to keep circuit open before attempting recovery (default: 30s).
    /// </summary>
    public int OpenDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Number of consecutive successes required to close circuit (default: 2).
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;
}
