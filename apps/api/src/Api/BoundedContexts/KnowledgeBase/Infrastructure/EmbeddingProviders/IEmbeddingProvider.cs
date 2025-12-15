

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;

/// <summary>
/// Abstraction for embedding generation providers.
/// Supports multiple backends: OpenRouter, Ollama, HuggingFace.
/// </summary>
internal interface IEmbeddingProvider
{
    /// <summary>
    /// Provider name (e.g., "OpenRouter", "Ollama", "HuggingFace")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Model identifier (e.g., "text-embedding-3-large", "nomic-embed-text")
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Vector dimensions produced by this provider/model
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Maximum context tokens supported
    /// </summary>
    int MaxContextTokens { get; }

    /// <summary>
    /// Generate embedding for a single text
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Embedding vector</returns>
    Task<EmbeddingProviderResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Generate embeddings for multiple texts (batch operation)
    /// </summary>
    /// <param name="texts">Texts to embed</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of embedding vectors</returns>
    Task<EmbeddingProviderResult> GenerateBatchEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default);

    /// <summary>
    /// Check if the provider is healthy and available
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

/// <summary>
/// Result of an embedding generation operation
/// </summary>
internal sealed record EmbeddingProviderResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<float[]> Embeddings { get; init; } = Array.Empty<float[]>();
    public string? Model { get; init; }
    public int? TokensUsed { get; init; }

    public static EmbeddingProviderResult CreateSuccess(IReadOnlyList<float[]> embeddings, string? model = null, int? tokensUsed = null) =>
        new() { Success = true, Embeddings = embeddings, Model = model, TokensUsed = tokensUsed };

    public static EmbeddingProviderResult CreateSuccess(float[] embedding, string? model = null, int? tokensUsed = null) =>
        new() { Success = true, Embeddings = new[] { embedding }, Model = model, TokensUsed = tokensUsed };

    public static EmbeddingProviderResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}
