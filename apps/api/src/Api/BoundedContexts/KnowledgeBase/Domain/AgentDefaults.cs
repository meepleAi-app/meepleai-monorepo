namespace Api.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Default configuration values for new agent instances.
/// Centralizes defaults to avoid hardcoded magic strings across handlers.
/// </summary>
internal static class AgentDefaults
{
    /// <summary>
    /// Default free-tier model (zero cost via OpenRouter).
    /// </summary>
    public const string DefaultFreeModel = "meta-llama/llama-3.3-70b-instruct:free";

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
}
