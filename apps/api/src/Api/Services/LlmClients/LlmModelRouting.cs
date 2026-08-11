namespace Api.Services.LlmClients;

/// <summary>
/// Shared model-id routing knowledge used by both <see cref="OllamaLlmClient"/> (to avoid
/// misrouting) and the agent-definition validators (to reject unroutable configs at write time),
/// so the two cannot drift.
/// </summary>
internal static class LlmModelRouting
{
    /// <summary>
    /// Bare model-id prefixes that belong to cloud providers Ollama never serves. Ambiguous names
    /// that also exist as local Ollama models (llama, mistral, qwen, phi, gemma, gpt-oss, …) are
    /// intentionally NOT listed — hence "gpt-3/4/5" rather than a bare "gpt" (which would wrongly
    /// reject the local "gpt-oss:20b/120b").
    /// </summary>
    internal static readonly string[] BareCloudProviderPrefixes =
    {
        "claude", "gpt-3", "gpt-4", "gpt-5", "chatgpt", "gemini", "grok", "o1-", "o3-", "o4-",
    };

    /// <summary>
    /// True when <paramref name="modelId"/> is a bare (unprefixed) cloud-provider id that no
    /// <see cref="ILlmClient"/> can route: OpenRouter needs a "provider/model" slug (a '/'),
    /// Ollama rejects these prefixes, and DeepSeek only serves "deepseek-*". Such an id makes
    /// <see cref="LlmProviderFactory"/> throw at chat time. Blank ids are NOT flagged here
    /// (NotEmpty validation covers them).
    /// </summary>
    public static bool IsUnroutableBareCloudId(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId) || modelId.Contains('/', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var prefix in BareCloudProviderPrefixes)
        {
            if (modelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
