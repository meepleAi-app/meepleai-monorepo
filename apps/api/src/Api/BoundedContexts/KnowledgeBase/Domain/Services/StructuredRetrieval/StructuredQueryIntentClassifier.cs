namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;

/// <summary>
/// Classifies user queries into structured intent categories for routing to RulebookAnalysis fields.
/// Uses keyword-based heuristics consistent with existing RoutingLlmPlugin patterns.
/// Issue #5453: Structured RAG fusion.
/// </summary>
internal sealed class StructuredQueryIntentClassifier
{
    private static readonly string[] VictoryKeywords =
        ["win", "winning", "victory", "score", "scoring", "end game", "endgame", "how to win", "game end", "points"];

    private static readonly string[] MechanicsKeywords =
        ["mechanic", "mechanics", "mechanism", "gameplay", "game play", "core mechanic", "game type"];

    private static readonly string[] GlossaryKeywords =
        ["what is", "what are", "define", "definition", "meaning of", "means", "term", "glossary", "concept"];

    private static readonly string[] FaqKeywords =
        ["can you", "can i", "is it possible", "how do you", "how do i", "when can", "when do", "allowed to", "able to"];

    /// <summary>
    /// Classifies a query into a structured intent with confidence score.
    /// </summary>
    public QueryIntentClassification Classify(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new QueryIntentClassification(StructuredQueryIntent.General, 0.0);

        var lowerQuery = query.ToLowerInvariant().Trim();

        // Check victory conditions first (highest specificity)
        var victoryScore = CalculateKeywordScore(lowerQuery, VictoryKeywords);
        if (victoryScore >= 0.6)
            return new QueryIntentClassification(StructuredQueryIntent.VictoryConditions, victoryScore);

        // Check glossary (concept lookup)
        var glossaryScore = CalculateKeywordScore(lowerQuery, GlossaryKeywords);
        if (glossaryScore >= 0.6)
        {
            // Extract the term being asked about
            var matchedTerm = ExtractGlossaryTerm(lowerQuery);
            return new QueryIntentClassification(StructuredQueryIntent.Glossary, glossaryScore, matchedTerm);
        }

        // Check FAQ patterns
        var faqScore = CalculateKeywordScore(lowerQuery, FaqKeywords);
        if (faqScore >= 0.5)
            return new QueryIntentClassification(StructuredQueryIntent.Faq, faqScore);

        // Check mechanics
        var mechanicsScore = CalculateKeywordScore(lowerQuery, MechanicsKeywords);
        if (mechanicsScore >= 0.5)
            return new QueryIntentClassification(StructuredQueryIntent.Mechanics, mechanicsScore);

        return new QueryIntentClassification(StructuredQueryIntent.General, 0.3);
    }

    private static double CalculateKeywordScore(string query, string[] keywords)
    {
        var matchCount = 0;
        var hasExactPhraseMatch = false;

        foreach (var keyword in keywords)
        {
            if (query.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
                // Multi-word phrases are stronger signals
                if (keyword.Contains(' '))
                    hasExactPhraseMatch = true;
            }
        }

        if (matchCount == 0) return 0.0;

        // Base score from match ratio, boosted for phrase matches
        var baseScore = Math.Min(1.0, 0.5 + (matchCount * 0.15));
        if (hasExactPhraseMatch)
            baseScore = Math.Min(1.0, baseScore + 0.15);

        return baseScore;
    }

    private static string? ExtractGlossaryTerm(string query)
    {
        // Patterns: "what is a/an X", "what are X", "define X", "meaning of X"
        var prefixes = new[] { "what is a ", "what is an ", "what is ", "what are ", "define ", "meaning of " };

        foreach (var prefix in prefixes)
        {
            if (query.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var term = query[prefix.Length..].Trim().TrimEnd('?');
                if (!string.IsNullOrWhiteSpace(term))
                    return term;
            }
        }

        return null;
    }
}
