namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Routing tier for a RAG query, derived from heuristic complexity analysis.
/// Drives LLM model selection in the RAG pipeline (P1-4 cost optimization).
/// Low → fast/free model; High → premium model; ~40% estimated cost reduction.
/// </summary>
public enum QueryRoutingTier { Low, Medium, High }

/// <summary>
/// Analyzes query text heuristically to determine a routing tier for LLM model selection.
/// Stateless and allocation-efficient: safe to register as Singleton.
/// </summary>
internal sealed class QueryComplexityAnalyzer
{
    private static readonly string[] HighComplexityKeywords =
        ["differenza", "confronta", "strategia", "ottimale", "perché", "compare",
         "difference", "strategy", "optimal", "why", "explain in detail", "analyze"];

    private static readonly string[] LowComplexityPrefixes =
        ["quanti", "quanto", "come si", "when", "where", "quando", "dove", "how many", "what is the"];

    /// <summary>
    /// Analyzes the given query and returns its routing tier.
    /// </summary>
    /// <param name="query">The user query text to analyze.</param>
    /// <returns>A <see cref="QueryRoutingTier"/> indicating which model tier to use.</returns>
    public QueryRoutingTier Analyze(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return QueryRoutingTier.Low;

        var lower = query.ToLowerInvariant();

        // Long queries are automatically high complexity
        if (query.Length > 200) return QueryRoutingTier.High;

        // Keywords indicating high complexity
        if (HighComplexityKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return QueryRoutingTier.High;

        // Short queries starting with low-complexity prefixes
        if (LowComplexityPrefixes.Any(k => lower.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            && query.Split(' ').Length <= 6)
            return QueryRoutingTier.Low;

        return QueryRoutingTier.Medium;
    }
}
