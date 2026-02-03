namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// RAG (Retrieval-Augmented Generation) strategy types for knowledge base queries.
/// Different strategies optimize for speed, accuracy, or balance between the two.
/// Issue #3436: TierStrategyAccess validation service.
/// </summary>
public enum RagStrategy
{
    /// <summary>
    /// No strategy - used for anonymous/restricted users.
    /// </summary>
    None = 0,

    /// <summary>
    /// Fast strategy: Optimized for speed with basic retrieval.
    /// Single-pass vector search with minimal reranking.
    /// Available to: User, Editor, Admin, Premium tiers.
    /// </summary>
    Fast = 1,

    /// <summary>
    /// Balanced strategy: Default strategy balancing speed and accuracy.
    /// Hybrid search (vector + keyword) with moderate reranking.
    /// Available to: User, Editor, Admin, Premium tiers.
    /// </summary>
    Balanced = 2,

    /// <summary>
    /// Precise strategy: Maximum accuracy with multi-pass retrieval.
    /// Full hybrid search with cross-encoder reranking and validation.
    /// Available to: Editor, Admin, Premium tiers.
    /// </summary>
    Precise = 3,

    /// <summary>
    /// Custom strategy: User-defined parameters for advanced users.
    /// Requires explicit configuration of all RAG parameters.
    /// Available to: Admin tier only.
    /// </summary>
    Custom = 4
}

/// <summary>
/// Extension methods for RagStrategy enum.
/// </summary>
public static class RagStrategyExtensions
{
    /// <summary>
    /// Gets a user-friendly display name for the strategy.
    /// </summary>
    public static string GetDisplayName(this RagStrategy strategy) => strategy switch
    {
        RagStrategy.None => "None",
        RagStrategy.Fast => "Fast",
        RagStrategy.Balanced => "Balanced",
        RagStrategy.Precise => "Precise",
        RagStrategy.Custom => "Custom",
        _ => strategy.ToString()
    };

    /// <summary>
    /// Gets a description of the strategy for user display.
    /// </summary>
    public static string GetDescription(this RagStrategy strategy) => strategy switch
    {
        RagStrategy.None => "No RAG strategy available",
        RagStrategy.Fast => "Optimized for speed with basic retrieval",
        RagStrategy.Balanced => "Default strategy balancing speed and accuracy",
        RagStrategy.Precise => "Maximum accuracy with multi-pass retrieval",
        RagStrategy.Custom => "User-defined parameters for advanced configuration",
        _ => "Unknown strategy"
    };

    /// <summary>
    /// Checks if the strategy requires admin privileges.
    /// </summary>
    public static bool RequiresAdmin(this RagStrategy strategy) =>
        strategy == RagStrategy.Custom;

    /// <summary>
    /// Parses a string to RagStrategy, case-insensitive.
    /// Returns None if parsing fails.
    /// </summary>
    public static RagStrategy ParseOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return RagStrategy.None;

        return Enum.TryParse<RagStrategy>(value, ignoreCase: true, out var strategy)
            ? strategy
            : RagStrategy.None;
    }
}
