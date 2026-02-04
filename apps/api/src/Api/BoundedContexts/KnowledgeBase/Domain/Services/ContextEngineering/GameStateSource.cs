using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Context source for game state snapshots (position similarity).
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// Retrieves similar game positions to provide contextual
/// board state information for AI agent decision-making.
/// </remarks>
internal sealed class GameStateSource : IContextSource
{
    private readonly IAgentGameStateSnapshotRepository _repository;
    private readonly ITokenEstimator _tokenEstimator;

    public GameStateSource(
        IAgentGameStateSnapshotRepository repository,
        ITokenEstimator tokenEstimator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tokenEstimator = tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator));
    }

    public string SourceId => "game_state";

    public string SourceName => "Game State";

    public int DefaultPriority => 90; // Highest priority for current state context

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

            // First get the latest snapshot for the current game state
            var latestSnapshot = await _repository.GetLatestByGameIdAsync(
                request.GameId.Value,
                cancellationToken).ConfigureAwait(false);

            var items = new List<RetrievedContextItem>();
            var totalTokens = 0;

            if (latestSnapshot != null)
            {
                var tokenCount = _tokenEstimator.EstimateTokens(latestSnapshot.BoardStateJson);
                if (tokenCount <= request.MaxTokens)
                {
                    items.Add(new RetrievedContextItem
                    {
                        Id = latestSnapshot.Id.ToString(),
                        Content = FormatGameState(latestSnapshot, isLatest: true),
                        Relevance = 1.0, // Latest is always most relevant
                        TokenCount = tokenCount,
                        ContentType = "current_state",
                        Timestamp = latestSnapshot.CreatedAt,
                        Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["game_id"] = latestSnapshot.GameId.ToString(),
                            ["turn_number"] = latestSnapshot.TurnNumber,
                            ["active_player"] = latestSnapshot.ActivePlayerId?.ToString() ?? "",
                            ["is_latest"] = true
                        }
                    });
                    totalTokens += tokenCount;
                }
            }

            // If we have query embedding and budget, find similar historical positions
            if (request.QueryEmbedding != null && totalTokens < request.MaxTokens)
            {
                var recentSnapshots = await _repository.GetByGameIdAsync(
                    request.GameId.Value,
                    request.MaxItems,
                    cancellationToken).ConfigureAwait(false);

                foreach (var snapshot in recentSnapshots)
                {
                    // Skip the latest (already included)
                    if (latestSnapshot != null && snapshot.Id == latestSnapshot.Id)
                        continue;

                    var tokenCount = _tokenEstimator.EstimateTokens(snapshot.BoardStateJson);
                    if (totalTokens + tokenCount > request.MaxTokens)
                        break;

                    // Calculate position similarity if embeddings available
                    var relevance = 0.5; // Default if no embedding
                    if (snapshot.Embedding != null)
                    {
                        relevance = snapshot.Embedding.CosineSimilarity(
                            new ValueObjects.Vector(request.QueryEmbedding));
                    }

                    if (relevance < request.MinRelevance)
                        continue;

                    items.Add(new RetrievedContextItem
                    {
                        Id = snapshot.Id.ToString(),
                        Content = FormatGameState(snapshot, isLatest: false),
                        Relevance = relevance,
                        TokenCount = tokenCount,
                        ContentType = "historical_state",
                        Timestamp = snapshot.CreatedAt,
                        Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["game_id"] = snapshot.GameId.ToString(),
                            ["turn_number"] = snapshot.TurnNumber,
                            ["active_player"] = snapshot.ActivePlayerId?.ToString() ?? "",
                            ["is_latest"] = false
                        }
                    });

                    totalTokens += tokenCount;
                }
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

        // Estimate ~200 tokens per board state JSON on average
        return Math.Min(count * 200, request.MaxTokens);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    private static string FormatGameState(Entities.AgentGameStateSnapshot snapshot, bool isLatest)
    {
        var header = isLatest
            ? $"[Current Game State - Turn {snapshot.TurnNumber}]"
            : $"[Historical State - Turn {snapshot.TurnNumber}]";

        return $"{header}\n{snapshot.BoardStateJson}";
    }
}
