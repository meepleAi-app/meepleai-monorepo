using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

#pragma warning disable MA0048 // File name must match type name - Contains evaluator with supporting records

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

internal sealed record ScoredChunk(string Id, string Text, float Score);

internal sealed record RelevanceClassification(string Verdict, float Confidence, string Reason);

internal sealed class RetrievalRelevanceEvaluator : IRetrievalRelevanceEvaluator
{
    private const float HighScoreThreshold = 0.85f;
    private const float LowScoreThreshold = 0.55f;

    private readonly ILlmService _llmService;
    private readonly ILogger<RetrievalRelevanceEvaluator> _logger;

    public RetrievalRelevanceEvaluator(ILlmService llmService, ILogger<RetrievalRelevanceEvaluator> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<RelevanceEvaluation> EvaluateAsync(
        string query, IReadOnlyList<ScoredChunk> chunks, CancellationToken ct = default)
    {
        if (chunks is null || chunks.Count == 0)
            return new RelevanceEvaluation(RelevanceVerdict.Incorrect, 1.0f, "No chunks to evaluate");

        var avgScore = chunks.Average(c => c.Score);

        // Tier 1: Heuristic — high confidence scores
        if (avgScore >= HighScoreThreshold)
            return new RelevanceEvaluation(RelevanceVerdict.Correct, avgScore, "High reranker scores");

        // Tier 1: Heuristic — very low scores
        if (avgScore < LowScoreThreshold)
            return new RelevanceEvaluation(RelevanceVerdict.Incorrect, 1.0f - avgScore, "Low reranker scores");

        // Tier 2: LLM fallback for borderline range
        return await EvaluateByLlmAsync(query, chunks, ct).ConfigureAwait(false);
    }

    private async Task<RelevanceEvaluation> EvaluateByLlmAsync(
        string query, IReadOnlyList<ScoredChunk> chunks, CancellationToken ct)
    {
        const string systemPrompt = """
            Evaluate whether the retrieved document chunks are relevant to the user's query.
            Respond with JSON: {"Verdict":"correct|ambiguous|incorrect","Confidence":0.0-1.0,"Reason":"brief reason"}
            - correct: chunks directly answer the query
            - ambiguous: chunks are partially relevant but may need supplementation
            - incorrect: chunks are not relevant to the query
            """;

        var chunkSummary = string.Join("\n---\n",
            chunks.Select(c => $"[Score={c.Score:F2}] {c.Text[..Math.Min(c.Text.Length, 200)]}"));
        var userPrompt = $"Query: {query}\n\nRetrieved chunks:\n{chunkSummary}";

        try
        {
            var result = await _llmService.GenerateJsonAsync<RelevanceClassification>(
                systemPrompt, userPrompt, RequestSource.RagClassification, ct).ConfigureAwait(false);

            if (result is not null)
                return MapClassification(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM relevance evaluation failed, defaulting to Correct (safe fallback)");
        }

        // Safe fallback: use whatever was retrieved
        return new RelevanceEvaluation(RelevanceVerdict.Correct, 0.5f, "LLM evaluation unavailable, safe default");
    }

    private static RelevanceEvaluation MapClassification(RelevanceClassification c)
    {
        var confidence = Math.Clamp(c.Confidence, 0f, 1f);
        var reason = string.IsNullOrWhiteSpace(c.Reason) ? "LLM evaluated" : c.Reason;

        var verdict = c.Verdict?.ToUpperInvariant() switch
        {
            "CORRECT" => RelevanceVerdict.Correct,
            "AMBIGUOUS" => RelevanceVerdict.Ambiguous,
            "INCORRECT" => RelevanceVerdict.Incorrect,
            _ => RelevanceVerdict.Ambiguous
        };

        return new RelevanceEvaluation(verdict, confidence, reason);
    }
}
