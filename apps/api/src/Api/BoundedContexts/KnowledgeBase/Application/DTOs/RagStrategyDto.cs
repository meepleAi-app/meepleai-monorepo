namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Public DTO for RAG strategy information.
/// Issue #8: Public endpoint for user/editor strategy selection.
/// </summary>
public record RagStrategyDto
{
    /// <summary>
    /// Strategy enum name (e.g., "Fast", "Balanced").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Display name for UI (e.g., "FAST", "BALANCED").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Human-readable description of the strategy.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Complexity level (0-11). Higher = more processing, higher cost.
    /// </summary>
    public required int Complexity { get; init; }

    /// <summary>
    /// Estimated tokens per query (approximate).
    /// </summary>
    public required int EstimatedTokens { get; init; }

    /// <summary>
    /// Whether this strategy requires admin privileges.
    /// </summary>
    public required bool RequiresAdmin { get; init; }

    /// <summary>
    /// Recommended use cases for this strategy.
    /// </summary>
    public required string UseCase { get; init; }
}
