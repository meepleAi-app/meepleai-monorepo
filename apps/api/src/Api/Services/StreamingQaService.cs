using Api.Infrastructure;
using Api.Models;
using Api.Observability;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Api.Services;

/// <summary>
/// CHAT-01: Streaming QA service for progressive answers via SSE with token-by-token delivery
/// AI-07.1: Now with advanced prompt engineering and few-shot learning
/// Emits events: StateUpdate -> Citations -> Token(s) -> Complete
/// </summary>
public class StreamingQaService : IStreamingQaService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly ILogger<StreamingQaService> _logger;

    public StreamingQaService(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILlmService llmService,
        IAiResponseCacheService cache,
        IPromptTemplateService promptTemplateService,
        ILogger<StreamingQaService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _llmService = llmService;
        _cache = cache;
        _promptTemplateService = promptTemplateService;
        _logger = logger;
    }

    public async IAsyncEnumerable<RagStreamingEvent> AskStreamAsync(
        string gameId,
        string query,
        Guid? chatId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("Please provide a question.", "EMPTY_QUERY"));
            yield break;
        }

        _logger.LogInformation("Starting streaming QA for game {GameId}, query: {Query}", gameId, query);

        // Check cache first - if cached, return it as streaming events
        var cacheKey = _cache.GenerateQaCacheKey(gameId, query);
        var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken);

        if (cachedResponse != null)
        {
            _logger.LogInformation("Returning cached QA response as stream for game {GameId}", gameId);

            // Emit state update
            yield return CreateEvent(StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Retrieved from cache"));

            // Emit citations
            yield return CreateEvent(StreamingEventType.Citations,
                new StreamingCitations(cachedResponse.snippets));

            // Emit answer as tokens (simulate streaming for consistency)
            var words = cachedResponse.answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var token = i == 0 ? words[i] : " " + words[i];
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(token));

                // Small delay to simulate streaming
                if (i < words.Length - 1)
                {
                    await Task.Delay(10, cancellationToken);
                }
            }

            // Emit complete
            yield return CreateEvent(StreamingEventType.Complete,
                new StreamingComplete(
                    0, // Not applicable for QA
                    cachedResponse.promptTokens,
                    cachedResponse.completionTokens,
                    cachedResponse.totalTokens,
                    cachedResponse.confidence));

            yield break;
        }

        // Stream events, catching any exceptions
        var stream = AskStreamInternalAsync(gameId, query, chatId, cancellationToken);

        await foreach (var evt in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> AskStreamInternalAsync(
        string gameId,
        string query,
        Guid? chatId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Step 1: Generate embedding
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating embeddings for query..."));

        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Failed to generate query embedding: {Error}", embeddingResult.ErrorMessage);
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("Unable to process query.", "EMBEDDING_FAILED"));
            yield break;
        }

        var queryEmbedding = embeddingResult.Embeddings[0];

        // Step 2: Search Qdrant
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching vector database for relevant content..."));

        var searchResult = await _qdrantService.SearchAsync(gameId, queryEmbedding, limit: 3, cancellationToken);

        if (!searchResult.Success || searchResult.Results.Count == 0)
        {
            _logger.LogInformation("No vector results found for query in game {GameId}", gameId);
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("No relevant information found in the rulebook.", "NO_RESULTS"));
            yield break;
        }

        // Step 3: Emit citations
        var snippets = searchResult.Results.Select(r => new Snippet(
            r.Text,
            $"PDF:{r.PdfId}",
            r.Page,
            0,
            r.Score // AI-11: Include actual search score for quality tracking
        )).ToList();

        yield return CreateEvent(StreamingEventType.Citations,
            new StreamingCitations(snippets));

        // Step 4: Build context and stream LLM response
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating answer..."));

        var context = string.Join("\n\n---\n\n", searchResult.Results.Select(r =>
            $"[Page {r.Page}]\n{r.Text}"));

        // AI-07.1: Use PromptTemplateService for advanced prompt engineering
        var questionType = _promptTemplateService.ClassifyQuestion(query);
        Guid? gameGuid = Guid.TryParse(gameId, out var guid) ? guid : null;
        var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType);

        var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
        var userPrompt = _promptTemplateService.RenderUserPrompt(template, context, query);

        _logger.LogDebug(
            "Using prompt template for game {GameId}, question type {QuestionType}",
            gameId, questionType);

        // Stream tokens from LLM
        var answerBuilder = new StringBuilder();
        var tokenCount = 0;

        await foreach (var token in _llmService.GenerateCompletionStreamAsync(systemPrompt, userPrompt, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            answerBuilder.Append(token);
            tokenCount++;
            yield return CreateEvent(StreamingEventType.Token, new StreamingToken(token));
        }

        var answer = answerBuilder.ToString().Trim();
        var confidence = searchResult.Results.Count > 0
            ? (double?)searchResult.Results.Max(r => r.Score)
            : null;

        _logger.LogInformation(
            "Streaming QA completed for game {GameId}, {TokenCount} tokens sent",
            gameId, tokenCount);

        // Build response for caching
        var response = new QaResponse(
            answer,
            snippets,
            0, // Token counts not available from streaming
            tokenCount,
            tokenCount,
            confidence,
            null);

        // Cache the complete response
        await _cache.SetAsync(_cache.GenerateQaCacheKey(gameId, query), response, 86400, cancellationToken);

        // Emit complete event
        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(
                0, // Not applicable for QA
                0,
                tokenCount,
                tokenCount,
                confidence));

        // Record metrics
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        MeepleAiMetrics.RecordRagRequest(duration, gameId, true);
        MeepleAiMetrics.TokensUsed.Record(tokenCount, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa-stream" } });
        if (confidence.HasValue)
        {
            MeepleAiMetrics.ConfidenceScore.Record(confidence.Value, new System.Diagnostics.TagList { { "game.id", gameId }, { "operation", "qa-stream" } });
        }
    }

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, DateTime.UtcNow);
    }
}
