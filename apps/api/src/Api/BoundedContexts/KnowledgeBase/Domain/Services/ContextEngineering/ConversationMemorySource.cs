using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Context source for conversation memory (temporal RAG).
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// Retrieves recent conversation history with temporal scoring
/// to provide contextual memory for AI agent responses.
/// </remarks>
internal sealed class ConversationMemorySource : IContextSource
{
    private readonly IConversationMemoryRepository _repository;
    private readonly ITokenEstimator _tokenEstimator;

    public ConversationMemorySource(
        IConversationMemoryRepository repository,
        ITokenEstimator tokenEstimator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tokenEstimator = tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator));
    }

    public string SourceId => "conversation_memory";

    public string SourceName => "Conversation Memory";

    public int DefaultPriority => 70; // High priority for recent context

    public async Task<ContextRetrievalResult> RetrieveAsync(
        ContextRetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            IReadOnlyList<Entities.ConversationMemory> memories;

            if (request.SessionId.HasValue)
            {
                var sessionMemories = await _repository.GetBySessionIdAsync(
                    request.SessionId.Value,
                    request.MaxItems,
                    cancellationToken).ConfigureAwait(false);
                memories = sessionMemories;
            }
            else if (request.UserId.HasValue && request.GameId.HasValue)
            {
                var gameMemories = await _repository.GetByUserAndGameAsync(
                    request.UserId.Value,
                    request.GameId.Value,
                    request.MaxItems,
                    cancellationToken).ConfigureAwait(false);
                memories = gameMemories;
            }
            else if (request.UserId.HasValue)
            {
                var userMemories = await _repository.GetRecentByUserIdAsync(
                    request.UserId.Value,
                    request.MaxItems,
                    cancellationToken).ConfigureAwait(false);
                memories = userMemories;
            }
            else
            {
                return ContextRetrievalResult.Empty(SourceId);
            }

            var items = new List<RetrievedContextItem>();
            var totalTokens = 0;
            var referenceTime = DateTime.UtcNow;
            var decayWindow = TimeSpan.FromHours(24); // Default 24-hour decay

            foreach (var memory in memories)
            {
                var tokenCount = _tokenEstimator.EstimateTokens(memory.Content);
                if (totalTokens + tokenCount > request.MaxTokens)
                    break;

                // Calculate temporal relevance score
                var temporalScore = memory.CalculateTemporalScore(referenceTime, decayWindow);

                // Combine with semantic relevance if embedding available
                var relevance = temporalScore;
                if (request.QueryEmbedding != null && memory.Embedding != null)
                {
                    var semanticScore = memory.Embedding.CosineSimilarity(
                        new ValueObjects.Vector(request.QueryEmbedding));
                    relevance = (0.7 * semanticScore) + (0.3 * temporalScore);
                }

                if (relevance < request.MinRelevance)
                    continue;

                items.Add(new RetrievedContextItem
                {
                    Id = memory.Id.ToString(),
                    Content = FormatMemoryContent(memory),
                    Relevance = relevance,
                    TokenCount = tokenCount,
                    ContentType = memory.MessageType,
                    Timestamp = memory.Timestamp,
                    Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["session_id"] = memory.SessionId.ToString(),
                        ["user_id"] = memory.UserId.ToString(),
                        ["game_id"] = memory.GameId?.ToString() ?? "",
                        ["message_type"] = memory.MessageType
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
        if (!request.UserId.HasValue)
            return 0;

        var count = await _repository.CountByUserIdAsync(
            request.UserId.Value,
            cancellationToken).ConfigureAwait(false);

        // Estimate ~50 tokens per message on average
        return Math.Min(count * 50, request.MaxTokens);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Repository is always available if DI resolved
        return Task.FromResult(true);
    }

    private static string FormatMemoryContent(Entities.ConversationMemory memory)
    {
        var role = memory.MessageType switch
        {
            "user" => "User",
            "assistant" => "Assistant",
            "system" => "System",
            _ => memory.MessageType
        };

        return $"[{role}]: {memory.Content}";
    }
}

/// <summary>
/// Interface for token count estimation.
/// </summary>
public interface ITokenEstimator
{
    /// <summary>
    /// Estimates the number of tokens in the given text.
    /// </summary>
    int EstimateTokens(string text);
}

/// <summary>
/// Simple token estimator based on word count heuristics.
/// </summary>
public sealed class SimpleTokenEstimator : ITokenEstimator
{
    private const double TokensPerWord = 1.3; // Average for GPT tokenizers
    private static readonly char[] WordSeparators = [' ', '\n', '\r', '\t'];

    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Count words approximately
        var wordCount = text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;

        return (int)Math.Ceiling(wordCount * TokensPerWord);
    }
}
