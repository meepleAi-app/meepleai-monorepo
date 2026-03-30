namespace Api.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Default configuration values for new agent instances.
/// Centralizes defaults to avoid hardcoded magic strings across handlers.
/// </summary>
internal static class AgentDefaults
{
    /// <summary>
    /// Default OpenRouter model. Reads from OPENROUTER_DEFAULT_MODEL env var at startup.
    /// Falls back to the paid Llama 3.3 70B model (not the :free variant which is rate-limited).
    /// </summary>
    public static readonly string DefaultModel =
        Environment.GetEnvironmentVariable("OPENROUTER_DEFAULT_MODEL")
        ?? "meta-llama/llama-3.3-70b-instruct";

    /// <summary>
    /// Default LLM provider: 0 = OpenRouter, 1 = Ollama.
    /// </summary>
    public const int DefaultLlmProvider = 0;

    /// <summary>
    /// Default temperature for agent responses.
    /// </summary>
    public const decimal DefaultTemperature = 0.3m;

    /// <summary>
    /// Default max tokens for agent responses.
    /// </summary>
    public const int DefaultMaxTokens = 2048;

    /// <summary>
    /// Local Ollama fallback model (zero cost).
    /// Reads from OLLAMA_DEFAULT_MODEL env var. Defaults to qwen2.5:1.5b.
    /// Used when free-tier OpenRouter models are rate-limited or unavailable.
    /// </summary>
    public static readonly string OllamaFallbackModel =
        Environment.GetEnvironmentVariable("OLLAMA_DEFAULT_MODEL") ?? "qwen2.5:1.5b";
}
