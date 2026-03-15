using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

#pragma warning disable MA0048 // File name must match type name - Contains classifier with supporting record

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

internal sealed record ComplexityClassification(string Level, float Confidence, string Reason);

internal sealed class QueryComplexityClassifier : IQueryComplexityClassifier
{
    private readonly ILlmService _llmService;
    private readonly ILogger<QueryComplexityClassifier> _logger;

    private static readonly string[] SimplePatterns =
    [
        "what is", "what's", "define", "who is", "who's",
        "how many", "when was", "when did", "where is", "where's"
    ];

    private static readonly string[] ComplexIndicators =
    [
        "compare", "after", "before", "if", "should i",
        "can i", "expansion", "strategy", "versus", "vs",
        "difference between", "pros and cons", "better than",
        "recommend", "best way"
    ];

    public QueryComplexityClassifier(ILlmService llmService, ILogger<QueryComplexityClassifier> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<QueryComplexity> ClassifyAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return QueryComplexity.Simple("Empty query", 1.0f);

        var heuristic = ClassifyByHeuristic(query);
        if (heuristic is not null)
            return heuristic;

        return await ClassifyByLlmAsync(query, ct).ConfigureAwait(false);
    }

    internal static QueryComplexity? ClassifyByHeuristic(string query)
    {
        var normalized = query.Trim().ToLowerInvariant();
        var wordCount = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        // Short queries with simple question prefixes -> Simple
        if (wordCount <= 6 && SimplePatterns.Any(p => normalized.StartsWith(p, StringComparison.Ordinal)))
            return QueryComplexity.Simple("Short definitional query", 0.95f);

        // Count complex indicators present in the query
        var complexCount = ComplexIndicators.Count(i =>
            normalized.Contains(i, StringComparison.Ordinal));

        if (complexCount >= 2)
            return QueryComplexity.Complex($"Multiple complex indicators ({complexCount})", 0.85f);

        // No strong signal -> null means fall through to LLM
        return null;
    }

    private async Task<QueryComplexity> ClassifyByLlmAsync(string query, CancellationToken ct)
    {
        const string systemPrompt = """
            Classify the complexity of the following board-game question.
            Respond with JSON: {"Level":"Simple|Moderate|Complex","Confidence":0.0-1.0,"Reason":"brief reason"}
            - Simple: single-fact lookup (e.g. "What is the max player count?")
            - Moderate: needs context retrieval from rules (e.g. "How does scoring work?")
            - Complex: multi-step reasoning, comparisons, or strategy advice
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<ComplexityClassification>(
                systemPrompt, query, RequestSource.RagClassification, ct).ConfigureAwait(false);

            if (result is not null)
                return MapClassification(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM classification failed for query, defaulting to Moderate");
        }

        // Safe fallback: Moderate ensures retrieval happens but skips multi-step
        return QueryComplexity.Moderate("LLM classification unavailable, safe default", 0.5f);
    }

    private static QueryComplexity MapClassification(ComplexityClassification c)
    {
        var confidence = Math.Clamp(c.Confidence, 0f, 1f);
        var reason = string.IsNullOrWhiteSpace(c.Reason) ? "LLM classified" : c.Reason;

        return c.Level?.ToUpperInvariant() switch
        {
            "SIMPLE" => QueryComplexity.Simple(reason, confidence),
            "MODERATE" => QueryComplexity.Moderate(reason, confidence),
            "COMPLEX" => QueryComplexity.Complex(reason, confidence),
            _ => QueryComplexity.Moderate(reason, confidence)
        };
    }

}
