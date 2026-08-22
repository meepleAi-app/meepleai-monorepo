namespace Api.Services;

internal interface IEmbeddingService
{
    Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default);
    Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default);

    // #3737: the two overloads above cannot say WHAT the embedding is for, and the e5 family
    // needs a different instruction prefix per side. They keep meaning "passage" — the
    // pre-#3737 behaviour — and the retrieval paths use the two below instead.
    //
    // Deliberately NOT expressed as an optional `purpose` parameter on the existing methods:
    // the callers pass the CancellationToken positionally (`GenerateEmbeddingAsync(query, ct)`),
    // so a parameter inserted before `ct` would not compile, and one appended after it would
    // put the token in the middle of the signature (CA1068). Separate overloads with a
    // REQUIRED purpose also mean no call site can end up on the wrong side by omission.

    /// <summary>
    /// Generate embeddings for texts, declaring which side of the retrieval pair they are.
    /// </summary>
    Task<EmbeddingResult> GenerateEmbeddingsAsync(List<string> texts, EmbeddingPurpose purpose, CancellationToken ct = default);

    /// <summary>
    /// Generate an embedding for a single text, declaring which side of the retrieval pair it is.
    /// Search queries MUST use <see cref="EmbeddingPurpose.Query"/>.
    /// </summary>
    Task<EmbeddingResult> GenerateEmbeddingAsync(string text, EmbeddingPurpose purpose, CancellationToken ct = default);

    /// <summary>
    /// Generate an embedding for a single text with both a language hint and a purpose.
    /// </summary>
    /// <remarks>
    /// Exists because two retrieval handlers (<c>SearchQueryHandler</c>,
    /// <c>AskQuestionQueryHandler</c>) already pass a language, and dropping it to reach the
    /// purpose-aware overload would silently discard a caller's stated intent — even though the
    /// current embedding service only validates <c>language</c> and does not act on it.
    /// </remarks>
    Task<EmbeddingResult> GenerateEmbeddingAsync(string text, string language, EmbeddingPurpose purpose, CancellationToken ct = default);

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
