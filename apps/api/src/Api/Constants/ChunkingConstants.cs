namespace Api.Constants;

/// <summary>
/// Centralized text chunking constants to eliminate magic numbers.
/// Used for RAG (Retrieval-Augmented Generation) text processing.
/// </summary>
internal static class ChunkingConstants
{
    /// <summary>
    /// Default chunk size in characters (512).
    /// Balanced size for most documents.
    /// </summary>
    public const int DefaultChunkSize = 512;

    /// <summary>
    /// Minimum chunk size in characters (256).
    /// Prevents overly small chunks that lack context.
    /// </summary>
    public const int MinChunkSize = 256;

    /// <summary>
    /// Maximum chunk size in characters (768).
    /// Prevents overly large chunks that exceed embedding model limits.
    /// </summary>
    public const int MaxChunkSize = 768;

    /// <summary>
    /// Default chunk overlap in characters (50).
    /// Overlap between consecutive chunks to maintain context continuity.
    /// </summary>
    public const int DefaultChunkOverlap = 50;

    /// <summary>
    /// Embedding token size limit (512 tokens).
    /// Maximum tokens for embedding models like sentence-transformers.
    /// </summary>
    public const int EmbeddingTokenLimit = 512;

    /// <summary>
    /// Approximate characters per token ratio (4:1).
    /// Used for estimating token count from character count.
    /// </summary>
    public const double CharsPerToken = 4.0;
}
