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
        if (HighComplexityKeywords.Any(k => ContainsWordOrPhrase(lower, k)))
            return QueryRoutingTier.High;

        // Short queries starting with low-complexity prefixes
        if (LowComplexityPrefixes.Any(k => lower.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            && lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 6)
            return QueryRoutingTier.Low;

        return QueryRoutingTier.Medium;
    }

    /// <summary>
    /// Returns true if <paramref name="lower"/> contains <paramref name="keyword"/> as a whole word or phrase.
    /// Multi-word phrases use a simple substring match (already specific enough).
    /// Single words are matched on word boundaries to avoid false positives
    /// (e.g., "why" inside "anyway", "strategy" inside "overstrategy").
    /// </summary>
    private static bool ContainsWordOrPhrase(string lower, string keyword)
    {
        // For multi-word phrases, use Contains directly (already specific enough)
        if (keyword.Contains(' ')) return lower.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        // For single words, match on word boundary (preceded/followed by non-letter or string boundary)
        var padded = " " + lower + " ";
        return padded.Contains(" " + keyword + " ", StringComparison.OrdinalIgnoreCase)
            || padded.Contains(" " + keyword + "?", StringComparison.OrdinalIgnoreCase)
            || padded.Contains(" " + keyword + "!", StringComparison.OrdinalIgnoreCase)
            || padded.Contains(" " + keyword + ",", StringComparison.OrdinalIgnoreCase)
            || padded.Contains(" " + keyword + ".", StringComparison.OrdinalIgnoreCase);
    }
}
