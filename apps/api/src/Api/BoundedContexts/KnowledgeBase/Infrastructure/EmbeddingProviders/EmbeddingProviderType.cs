

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;

/// <summary>
/// Supported embedding provider types per ADR-016 Phase 2.
/// </summary>
public enum EmbeddingProviderType
{
    /// <summary>
    /// OpenRouter with text-embedding-3-large (3072 dimensions, 8K context)
    /// Production use, $0.13/M tokens
    /// </summary>
    OpenRouterLarge,

    /// <summary>
    /// OpenRouter with text-embedding-3-small (1536 dimensions, 8K context)
    /// Budget option, $0.02/M tokens
    /// </summary>
    OpenRouterSmall,

    /// <summary>
    /// Ollama with nomic-embed-text (768 dimensions, 8K context)
    /// Local development, free
    /// </summary>
    OllamaNomic,

    /// <summary>
    /// Ollama with mxbai-embed-large (1024 dimensions, 512 context)
    /// Fallback option, free
    /// </summary>
    OllamaMxbai,

    /// <summary>
    /// HuggingFace with BGE-M3 (1024 dimensions, 8K context)
    /// Multilingual support, free
    /// </summary>
    HuggingFaceBgeM3
}

/// <summary>
/// Extension methods for EmbeddingProviderType
/// </summary>
public static class EmbeddingProviderTypeExtensions
{
    /// <summary>
    /// Get the model name for a provider type
    /// </summary>
    public static string GetModelName(this EmbeddingProviderType type) => type switch
    {
        EmbeddingProviderType.OpenRouterLarge => "text-embedding-3-large",
        EmbeddingProviderType.OpenRouterSmall => "text-embedding-3-small",
        EmbeddingProviderType.OllamaNomic => "nomic-embed-text",
        EmbeddingProviderType.OllamaMxbai => "mxbai-embed-large",
        EmbeddingProviderType.HuggingFaceBgeM3 => "BAAI/bge-m3",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown provider type")
    };

    /// <summary>
    /// Get the vector dimensions for a provider type
    /// </summary>
    public static int GetDimensions(this EmbeddingProviderType type) => type switch
    {
        EmbeddingProviderType.OpenRouterLarge => 3072,
        EmbeddingProviderType.OpenRouterSmall => 1536,
        EmbeddingProviderType.OllamaNomic => 768,
        EmbeddingProviderType.OllamaMxbai => 1024,
        EmbeddingProviderType.HuggingFaceBgeM3 => 1024,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown provider type")
    };

    /// <summary>
    /// Get the maximum context tokens for a provider type
    /// </summary>
    public static int GetMaxContextTokens(this EmbeddingProviderType type) => type switch
    {
        EmbeddingProviderType.OpenRouterLarge => 8191,
        EmbeddingProviderType.OpenRouterSmall => 8191,
        EmbeddingProviderType.OllamaNomic => 8192,
        EmbeddingProviderType.OllamaMxbai => 512,
        EmbeddingProviderType.HuggingFaceBgeM3 => 8192,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown provider type")
    };

    /// <summary>
    /// Parse a string to EmbeddingProviderType
    /// </summary>
    public static EmbeddingProviderType? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.ToUpperInvariant() switch
        {
            "OPENROUTERLARGE" or "OPENROUTER_LARGE" or "OPENROUTER-LARGE" => EmbeddingProviderType.OpenRouterLarge,
            "OPENROUTERSMALL" or "OPENROUTER_SMALL" or "OPENROUTER-SMALL" => EmbeddingProviderType.OpenRouterSmall,
            "OLLAMANOMIC" or "OLLAMA_NOMIC" or "OLLAMA-NOMIC" or "OLLAMA" => EmbeddingProviderType.OllamaNomic,
            "OLLAMAMXBAI" or "OLLAMA_MXBAI" or "OLLAMA-MXBAI" => EmbeddingProviderType.OllamaMxbai,
            "HUGGINGFACEBGEM3" or "HUGGINGFACE_BGE_M3" or "HUGGINGFACE-BGE-M3" or "BGE-M3" or "BGEM3" => EmbeddingProviderType.HuggingFaceBgeM3,
            _ => null
        };
    }
}
