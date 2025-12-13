namespace Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

/// <summary>
/// ADR-016 Phase 1: Configuration for chunking strategies.
/// Value Object - immutable, equality by value.
/// </summary>
public sealed record ChunkingConfiguration
{
    /// <summary>
    /// Configuration name identifier.
    /// </summary>
    public string Name { get; init; } = "baseline";

    /// <summary>
    /// Target chunk size in tokens.
    /// </summary>
    public int ChunkSizeTokens { get; init; } = 350;

    /// <summary>
    /// Overlap percentage (0.0 to 1.0).
    /// </summary>
    public double OverlapPercentage { get; init; } = 0.15;

    /// <summary>
    /// Calculated overlap in tokens.
    /// </summary>
    public int OverlapTokens => (int)(ChunkSizeTokens * OverlapPercentage);

    /// <summary>
    /// Approximate characters per token ratio.
    /// </summary>
    public double CharsPerToken { get; init; } = 4.0;

    /// <summary>
    /// Calculated chunk size in characters.
    /// </summary>
    public int ChunkSizeChars => (int)(ChunkSizeTokens * CharsPerToken);

    /// <summary>
    /// Calculated overlap in characters.
    /// </summary>
    public int OverlapChars => (int)(OverlapTokens * CharsPerToken);

    /// <summary>
    /// Whether to respect sentence boundaries.
    /// </summary>
    public bool RespectSentenceBoundaries { get; init; } = true;

    /// <summary>
    /// Whether to respect paragraph boundaries (higher priority than sentences).
    /// </summary>
    public bool RespectParagraphBoundaries { get; init; } = true;

    /// <summary>
    /// Baseline configuration for general rulebooks (350 tokens, 15% overlap).
    /// </summary>
    public static ChunkingConfiguration Baseline => new()
    {
        Name = "baseline",
        ChunkSizeTokens = 350,
        OverlapPercentage = 0.15
    };

    /// <summary>
    /// Dense configuration for complex tables/lists (200 tokens, 20% overlap).
    /// </summary>
    public static ChunkingConfiguration Dense => new()
    {
        Name = "dense",
        ChunkSizeTokens = 200,
        OverlapPercentage = 0.20
    };

    /// <summary>
    /// Sparse configuration for narrative sections (500 tokens, 10% overlap).
    /// </summary>
    public static ChunkingConfiguration Sparse => new()
    {
        Name = "sparse",
        ChunkSizeTokens = 500,
        OverlapPercentage = 0.10
    };

    /// <summary>
    /// Validates configuration parameters.
    /// </summary>
    /// <returns>True if valid, throws otherwise.</returns>
    public bool Validate()
    {
        // MA0015: Use InvalidOperationException for property validation (not parameter validation)
        if (ChunkSizeTokens <= 0)
            throw new InvalidOperationException($"{nameof(ChunkSizeTokens)} must be positive");

        if (OverlapPercentage is < 0 or > 0.5)
            throw new InvalidOperationException($"{nameof(OverlapPercentage)} must be between 0 and 0.5");

        if (CharsPerToken <= 0)
            throw new InvalidOperationException($"{nameof(CharsPerToken)} must be positive");

        return true;
    }
}
