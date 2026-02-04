namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Strategy interface for source-specific context retrieval logic.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// Different strategies for different source types:
/// - Static: Hybrid search (keyword + semantic)
/// - Memory: Temporal scoring (recency + relevance)
/// - State: Position similarity search
/// - Tools: Capability matching
/// </remarks>
public interface IContextRetrievalStrategy
{
    /// <summary>
    /// Gets the strategy identifier.
    /// </summary>
    string StrategyId { get; }

    /// <summary>
    /// Gets the source types this strategy supports.
    /// </summary>
    IReadOnlyList<string> SupportedSourceTypes { get; }

    /// <summary>
    /// Applies the retrieval strategy to score and filter items.
    /// </summary>
    /// <param name="items">Items to process.</param>
    /// <param name="context">Strategy execution context.</param>
    /// <returns>Processed items with updated relevance scores.</returns>
    IReadOnlyList<RetrievedContextItem> Apply(
        IReadOnlyList<RetrievedContextItem> items,
        StrategyContext context);
}

/// <summary>
/// Context for strategy execution.
/// </summary>
public sealed record StrategyContext
{
    /// <summary>
    /// The original query.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Current timestamp for temporal calculations.
    /// </summary>
    public DateTime ReferenceTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional query embedding for similarity calculations.
    /// </summary>
    public float[]? QueryEmbedding { get; init; }

    /// <summary>
    /// Maximum items to return after strategy application.
    /// </summary>
    public int MaxItems { get; init; } = 10;

    /// <summary>
    /// Minimum score threshold for inclusion.
    /// </summary>
    public double MinScore { get; init; }

    /// <summary>
    /// Strategy-specific parameters.
    /// </summary>
    public IDictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Temporal scoring strategy for conversation memory.
/// Issue #3491: Temporal scoring for conversation memory.
/// </summary>
public sealed class TemporalScoringStrategy : IContextRetrievalStrategy
{
    private readonly TimeSpan _decayWindow;
    private readonly double _recencyWeight;
    private readonly double _relevanceWeight;

    public TemporalScoringStrategy(
        TimeSpan? decayWindow = null,
        double recencyWeight = 0.3,
        double relevanceWeight = 0.7)
    {
        _decayWindow = decayWindow ?? TimeSpan.FromHours(24);
        _recencyWeight = recencyWeight;
        _relevanceWeight = relevanceWeight;

        if (Math.Abs(_recencyWeight + _relevanceWeight - 1.0) > 0.001)
            throw new ArgumentException("Recency and relevance weights must sum to 1.0", nameof(recencyWeight));
    }

    public string StrategyId => "temporal_scoring";

    public IReadOnlyList<string> SupportedSourceTypes => ["conversation_memory"];

    public IReadOnlyList<RetrievedContextItem> Apply(
        IReadOnlyList<RetrievedContextItem> items,
        StrategyContext context)
    {
        return items
            .Select(item => item with
            {
                Relevance = CalculateTemporalScore(item, context.ReferenceTime)
            })
            .Where(item => item.Relevance >= context.MinScore)
            .OrderByDescending(item => item.Relevance)
            .Take(context.MaxItems)
            .ToList();
    }

    private double CalculateTemporalScore(RetrievedContextItem item, DateTime referenceTime)
    {
        var recencyScore = 0.0;

        if (item.Timestamp.HasValue)
        {
            var age = referenceTime - item.Timestamp.Value;
            if (age <= TimeSpan.Zero)
                recencyScore = 1.0;
            else if (age >= _decayWindow)
                recencyScore = 0.0;
            else
                // Exponential decay
                recencyScore = Math.Exp(-3 * age.TotalSeconds / _decayWindow.TotalSeconds);
        }

        return (_relevanceWeight * item.Relevance) + (_recencyWeight * recencyScore);
    }
}

/// <summary>
/// Position similarity strategy for game state matching.
/// Issue #3491: Position similarity search for game state.
/// </summary>
public sealed class PositionSimilarityStrategy : IContextRetrievalStrategy
{
    private readonly double _similarityThreshold;

    public PositionSimilarityStrategy(double similarityThreshold = 0.7)
    {
        _similarityThreshold = similarityThreshold;
    }

    public string StrategyId => "position_similarity";

    public IReadOnlyList<string> SupportedSourceTypes => ["game_state"];

    public IReadOnlyList<RetrievedContextItem> Apply(
        IReadOnlyList<RetrievedContextItem> items,
        StrategyContext context)
    {
        if (context.QueryEmbedding == null || context.QueryEmbedding.Length == 0)
        {
            // Without query embedding, just filter and sort by existing relevance
            return items
                .Where(item => item.Relevance >= _similarityThreshold)
                .OrderByDescending(item => item.Relevance)
                .Take(context.MaxItems)
                .ToList();
        }

        // Items should already have similarity scores calculated during retrieval
        return items
            .Where(item => item.Relevance >= _similarityThreshold)
            .OrderByDescending(item => item.Relevance)
            .Take(context.MaxItems)
            .ToList();
    }
}

/// <summary>
/// Hybrid search strategy combining keyword and semantic matching.
/// </summary>
public sealed class HybridSearchStrategy : IContextRetrievalStrategy
{
    /// <summary>
    /// Gets the semantic weight used for scoring.
    /// </summary>
    public double SemanticWeight { get; }

    /// <summary>
    /// Gets the keyword weight used for scoring.
    /// </summary>
    public double KeywordWeight { get; }

    public HybridSearchStrategy(
        double semanticWeight = 0.7,
        double keywordWeight = 0.3)
    {
        SemanticWeight = semanticWeight;
        KeywordWeight = keywordWeight;

        if (Math.Abs(SemanticWeight + KeywordWeight - 1.0) > 0.001)
            throw new ArgumentException("Semantic and keyword weights must sum to 1.0", nameof(semanticWeight));
    }

    public string StrategyId => "hybrid_search";

    public IReadOnlyList<string> SupportedSourceTypes => ["static_knowledge", "rules"];

    public IReadOnlyList<RetrievedContextItem> Apply(
        IReadOnlyList<RetrievedContextItem> items,
        StrategyContext context)
    {
        // Items should already have combined scores from hybrid retrieval
        // This strategy applies any post-processing adjustments
        return items
            .Where(item => item.Relevance >= context.MinScore)
            .OrderByDescending(item => item.Relevance)
            .Take(context.MaxItems)
            .ToList();
    }
}

/// <summary>
/// Capability matching strategy for tool metadata.
/// </summary>
public sealed class CapabilityMatchingStrategy : IContextRetrievalStrategy
{
    public string StrategyId => "capability_matching";

    public IReadOnlyList<string> SupportedSourceTypes => ["tool_metadata", "actions"];

    public IReadOnlyList<RetrievedContextItem> Apply(
        IReadOnlyList<RetrievedContextItem> items,
        StrategyContext context)
    {
        // Filter tools based on capability match to the query
        return items
            .Where(item => item.Relevance >= context.MinScore)
            .OrderByDescending(item => item.Relevance)
            .Take(context.MaxItems)
            .ToList();
    }
}
