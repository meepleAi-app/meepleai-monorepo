using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Context source for cached strategy patterns.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// Retrieves pre-computed strategy patterns relevant to the current
/// game phase and position for AI agent decision-making.
/// </remarks>
internal sealed class StrategyPatternSource : IContextSource
{
    private readonly IStrategyPatternRepository _repository;
    private readonly ITokenEstimator _tokenEstimator;

    public StrategyPatternSource(
        IStrategyPatternRepository repository,
        ITokenEstimator tokenEstimator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tokenEstimator = tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator));
    }

    public string SourceId => "strategy_patterns";

    public string SourceName => "Strategy Patterns";

    public int DefaultPriority => 60; // Medium-high priority for strategic guidance

    public async Task<ContextRetrievalResult> RetrieveAsync(
        ContextRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!request.GameId.HasValue)
            {
                return ContextRetrievalResult.Empty(SourceId);
            }

            var items = new List<RetrievedContextItem>();
            var totalTokens = 0;

            // Get phase from metadata if available
            string? phase = null;
            if (request.Metadata?.TryGetValue("game_phase", out var phaseValue) == true)
            {
                phase = phaseValue?.ToString();
            }

            IReadOnlyList<Entities.StrategyPattern> patterns;

            if (!string.IsNullOrEmpty(phase))
            {
                // Get patterns for specific game phase
                var phasePatterns = await _repository.GetByGameAndPhaseAsync(
                    request.GameId.Value,
                    phase,
                    request.MaxItems,
                    cancellationToken).ConfigureAwait(false);
                patterns = phasePatterns;
            }
            else
            {
                // Get top-rated patterns for the game
                var topPatterns = await _repository.GetTopRatedByGameIdAsync(
                    request.GameId.Value,
                    request.MaxItems,
                    cancellationToken).ConfigureAwait(false);
                patterns = topPatterns;
            }

            foreach (var pattern in patterns)
            {
                var content = FormatStrategyPattern(pattern);
                var tokenCount = _tokenEstimator.EstimateTokens(content);

                if (totalTokens + tokenCount > request.MaxTokens)
                    break;

                // Calculate relevance based on score and/or embedding similarity
                var relevance = pattern.EvaluationScore.HasValue
                    ? (pattern.EvaluationScore.Value + 1) / 2 // Normalize -1..1 to 0..1
                    : 0.5;

                if (request.QueryEmbedding != null && pattern.Embedding != null)
                {
                    var semanticScore = pattern.Embedding.CosineSimilarity(
                        new ValueObjects.Vector(request.QueryEmbedding));
                    // Combine score-based and semantic relevance
                    relevance = (0.5 * relevance) + (0.5 * semanticScore);
                }

                if (relevance < request.MinRelevance)
                    continue;

                items.Add(new RetrievedContextItem
                {
                    Id = pattern.Id.ToString(),
                    Content = content,
                    Relevance = relevance,
                    TokenCount = tokenCount,
                    ContentType = "strategy_pattern",
                    Timestamp = pattern.UpdatedAt ?? pattern.CreatedAt,
                    Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["game_id"] = pattern.GameId.ToString(),
                        ["pattern_name"] = pattern.PatternName,
                        ["phase"] = pattern.ApplicablePhase ?? "",
                        ["score"] = pattern.EvaluationScore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                        ["source"] = pattern.Source ?? ""
                    }
                });

                totalTokens += tokenCount;
            }

            stopwatch.Stop();

            return new ContextRetrievalResult
            {
                SourceId = SourceId,
                Items = items.OrderByDescending(i => i.Relevance).ToList(),
                TotalTokens = totalTokens,
                RetrievalDurationMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ContextRetrievalResult.Failure(SourceId, ex.Message);
        }
    }

    public async Task<int> EstimateTokensAsync(
        ContextRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.GameId.HasValue)
            return 0;

        var count = await _repository.CountByGameIdAsync(
            request.GameId.Value,
            cancellationToken).ConfigureAwait(false);

        // Estimate ~100 tokens per pattern on average
        return Math.Min(count * 100, request.MaxTokens);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    private static string FormatStrategyPattern(Entities.StrategyPattern pattern)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"**{pattern.PatternName}**");

        if (!string.IsNullOrEmpty(pattern.ApplicablePhase))
            sb.AppendLine($"Phase: {pattern.ApplicablePhase}");

        if (!string.IsNullOrEmpty(pattern.Description))
            sb.AppendLine($"Description: {pattern.Description}");

        if (pattern.EvaluationScore.HasValue)
            sb.AppendLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Score: {pattern.EvaluationScore.Value:F2}"));

        if (!string.IsNullOrEmpty(pattern.BoardConditionsJson))
            sb.AppendLine($"Conditions: {pattern.BoardConditionsJson}");

        if (!string.IsNullOrEmpty(pattern.MoveSequenceJson))
            sb.AppendLine($"Moves: {pattern.MoveSequenceJson}");

        if (!string.IsNullOrEmpty(pattern.Source))
            sb.AppendLine($"Source: {pattern.Source}");

        return sb.ToString().TrimEnd();
    }
}
