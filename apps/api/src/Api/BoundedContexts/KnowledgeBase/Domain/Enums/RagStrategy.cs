namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Defines the available RAG (Retrieval-Augmented Generation) strategies.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
/// <remarks>
/// Strategy hierarchy (complexity/cost):
/// FAST → BALANCED → PRECISE → EXPERT → CONSENSUS → CUSTOM
///
/// Each strategy defines:
/// - Required processing phases
/// - Model quality expectations
/// - Typical use cases
/// </remarks>
public enum RagStrategy
{
    /// <summary>
    /// Fast, simple queries with minimal processing.
    /// Phases: Synthesis only.
    /// Use case: Quick lookups, simple Q&amp;A.
    /// </summary>
    Fast = 0,

    /// <summary>
    /// Balanced approach with CRAG evaluation.
    /// Phases: Synthesis + CragEvaluation.
    /// Use case: Standard gameplay questions.
    /// </summary>
    Balanced = 1,

    /// <summary>
    /// High-precision multi-agent validation.
    /// Phases: Retrieval, Analysis, Synthesis, Validation.
    /// Use case: Complex rules interpretation.
    /// </summary>
    Precise = 2,

    /// <summary>
    /// Expert mode with web search and multi-hop reasoning.
    /// Phases: WebSearch, MultiHop, Synthesis.
    /// Use case: Research, obscure rules, clarifications.
    /// </summary>
    Expert = 3,

    /// <summary>
    /// Multi-model consensus voting.
    /// Phases: Multiple LLM voters + aggregator.
    /// Use case: Critical decisions, disputed interpretations.
    /// </summary>
    Consensus = 4,

    /// <summary>
    /// Sentence Window strategy with overlapping document windows.
    /// Phases: WindowSplitting, Retrieval, WindowExpansion, Synthesis.
    /// Use case: Precise rule citations, context-aware answers.
    /// +7% accuracy, ~3,250 tokens per query.
    /// </summary>
    SentenceWindow = 5,

    /// <summary>
    /// Admin-defined custom strategy.
    /// Phases: Configurable (minimum: Synthesis).
    /// Use case: Specialized workflows, testing.
    /// </summary>
    Custom = 6
}

/// <summary>
/// Extension methods for RagStrategy enum.
/// </summary>
public static class RagStrategyExtensions
{
    /// <summary>
    /// Gets the display name for a strategy.
    /// </summary>
    public static string GetDisplayName(this RagStrategy strategy) => strategy switch
    {
        RagStrategy.Fast => "FAST",
        RagStrategy.Balanced => "BALANCED",
        RagStrategy.Precise => "PRECISE",
        RagStrategy.Expert => "EXPERT",
        RagStrategy.Consensus => "CONSENSUS",
        RagStrategy.SentenceWindow => "SENTENCE_WINDOW",
        RagStrategy.Custom => "CUSTOM",
        _ => strategy.ToString().ToUpperInvariant()
    };

    /// <summary>
    /// Parses a string to RagStrategy enum.
    /// </summary>
    /// <param name="value">The string value to parse (case-insensitive).</param>
    /// <returns>The parsed RagStrategy.</returns>
    /// <exception cref="ArgumentException">Thrown when value is not a valid strategy.</exception>
    public static RagStrategy Parse(string value)
    {
        if (TryParse(value, out var strategy))
            return strategy;

        throw new ArgumentException(
            $"Invalid RAG strategy: '{value}'. Valid values are: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, SENTENCE_WINDOW, CUSTOM",
            nameof(value));
    }

    /// <summary>
    /// Tries to parse a string to RagStrategy enum.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="strategy">The parsed strategy if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(string? value, out RagStrategy strategy)
    {
        strategy = RagStrategy.Fast;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Normalize: FAST -> Fast, fast -> Fast
        var normalized = value.Trim();
        return Enum.TryParse(normalized, ignoreCase: true, out strategy);
    }

    /// <summary>
    /// Gets the complexity level of a strategy (0-5).
    /// Higher complexity = more processing, higher cost.
    /// </summary>
    public static int GetComplexityLevel(this RagStrategy strategy) => (int)strategy;

    /// <summary>
    /// Checks if a strategy requires admin privileges.
    /// </summary>
    public static bool RequiresAdmin(this RagStrategy strategy) => strategy == RagStrategy.Custom;
}
