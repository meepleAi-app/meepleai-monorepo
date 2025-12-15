using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;

/// <summary>
/// Configuration for embedding providers per ADR-016 Phase 2.
/// Supports primary provider, fallback, and batch settings.
/// </summary>
internal sealed class EmbeddingConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Embedding";

    /// <summary>
    /// Primary embedding provider type
    /// </summary>
    [Required]
    public EmbeddingProviderType Provider { get; set; } = EmbeddingProviderType.OllamaNomic;

    /// <summary>
    /// Fallback provider when primary fails (optional)
    /// </summary>
    public EmbeddingProviderType? FallbackProvider { get; set; }

    /// <summary>
    /// Maximum batch size for batch embedding operations
    /// </summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum retry attempts before falling back
    /// </summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable fallback chain when primary provider fails
    /// </summary>
    public bool EnableFallback { get; set; } = true;

    // Provider-specific configuration

    /// <summary>
    /// Ollama server URL (default: http://localhost:11434)
    /// </summary>
    public string OllamaUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// OpenRouter API key (required for OpenRouter providers)
    /// </summary>
    public string? OpenRouterApiKey { get; set; }

    /// <summary>
    /// HuggingFace API endpoint (for BGE-M3)
    /// </summary>
    public string? HuggingFaceEndpoint { get; set; }

    /// <summary>
    /// HuggingFace API key (optional, for rate limits)
    /// </summary>
    public string? HuggingFaceApiKey { get; set; }

    /// <summary>
    /// Override dimensions (uses model default if not set)
    /// </summary>
    public int? Dimensions { get; set; }

    /// <summary>
    /// Override model name (uses provider default if not set)
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Get effective dimensions for the configured provider
    /// </summary>
    public int GetEffectiveDimensions() =>
        Dimensions ?? Provider.GetDimensions();

    /// <summary>
    /// Get effective model name for the configured provider
    /// </summary>
    public string GetEffectiveModel() =>
        Model ?? Provider.GetModelName();

    /// <summary>
    /// Validate configuration
    /// </summary>
    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        // OpenRouter requires API key
        if ((Provider == EmbeddingProviderType.OpenRouterLarge || Provider == EmbeddingProviderType.OpenRouterSmall)
            && string.IsNullOrWhiteSpace(OpenRouterApiKey))
        {
            errors.Add("OpenRouter provider requires OpenRouterApiKey to be configured");
        }

        // Fallback provider should be different from primary
        if (FallbackProvider.HasValue && FallbackProvider.Value == Provider)
        {
            errors.Add("FallbackProvider must be different from primary Provider");
        }

        // Fallback OpenRouter also needs API key
        if (EnableFallback && FallbackProvider.HasValue
            && (FallbackProvider.Value == EmbeddingProviderType.OpenRouterLarge || FallbackProvider.Value == EmbeddingProviderType.OpenRouterSmall)
            && string.IsNullOrWhiteSpace(OpenRouterApiKey))
        {
            errors.Add("OpenRouter fallback provider requires OpenRouterApiKey to be configured");
        }

        return errors;
    }
}
