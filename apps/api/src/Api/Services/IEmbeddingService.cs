namespace Api.Services;

internal interface IEmbeddingService
{
    Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default);
    Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Get the configured embedding dimensions for the current model
    /// </summary>
    int GetEmbeddingDimensions();

    /// <summary>
    /// Get the configured embedding model name
    /// </summary>
    string GetModelName();

    // AI-09: Multi-language support
    /// <summary>
    /// Generate embeddings for texts with language-specific model selection
    /// </summary>
    /// <param name="texts">Texts to embed</param>
    /// <param name="language">ISO 639-1 language code (en, it, de, fr, es)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Embedding result with language-appropriate vectors</returns>
    Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, string language, CancellationToken ct = default);

    /// <summary>
    /// Generate embedding for a single text with language-specific model
    /// </summary>
    Task<EmbeddingResult> GenerateEmbeddingAsync(string text, string language, CancellationToken ct = default);
}
