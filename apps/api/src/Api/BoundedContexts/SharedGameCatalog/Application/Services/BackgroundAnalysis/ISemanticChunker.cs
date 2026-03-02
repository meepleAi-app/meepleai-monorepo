using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Service for semantic chunking of rulebook content using embeddings.
/// Second phase of multi-phase background analysis.
/// </summary>
public interface ISemanticChunker
{
    /// <summary>
    /// Chunks rulebook content semantically based on section boundaries and embeddings.
    /// </summary>
    /// <param name="rulebookContent">Full rulebook text</param>
    /// <param name="sectionHeaders">Section headers from overview extraction (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of semantic chunks with context</returns>
    Task<SemanticChunkingResult> ChunkAsync(
        string rulebookContent,
        List<string>? sectionHeaders = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of semantic chunking phase.
/// </summary>
public sealed record SemanticChunkingResult
{
    public List<SemanticChunk> Chunks { get; init; } = [];
    public int TotalChunks { get; init; }
    public int TotalCharacters { get; init; }
    public ChunkingStrategy StrategyUsed { get; init; }

    public static SemanticChunkingResult Create(
        List<SemanticChunk> chunks,
        ChunkingStrategy strategy) => new()
        {
            Chunks = chunks,
            TotalChunks = chunks.Count,
            TotalCharacters = chunks.Sum(c => c.CharacterCount),
            StrategyUsed = strategy
        };
}

/// <summary>
/// Chunking strategy used.
/// </summary>
public enum ChunkingStrategy
{
    /// <summary>Embedding-based semantic boundaries</summary>
    EmbeddingBased,

    /// <summary>Header-based section splits</summary>
    HeaderBased,

    /// <summary>Fixed-size with overlap (fallback)</summary>
    FixedSize
}
