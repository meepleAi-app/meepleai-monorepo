using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Helpers;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for StreamQaQuery.
/// Implements streaming RAG Q&A with token-by-token delivery and chat context integration.
/// CHAT-01: Integrates with ChatContextDomainService for conversation continuity
/// AI-11: Tracks quality metrics and confidence scoring
/// </summary>
public class StreamQaQueryHandler : IStreamingQueryHandler<StreamQaQuery, RagStreamingEvent>
{
    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly ILogger<StreamQaQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public StreamQaQueryHandler(
        SearchQueryHandler searchQueryHandler,
        QualityTrackingDomainService qualityTrackingService,
        ChatContextDomainService chatContextService,
        IChatThreadRepository chatThreadRepository,
        ILlmService llmService,
        IAiResponseCacheService cache,
        IPromptTemplateService promptTemplateService,
        ILogger<StreamQaQueryHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _searchQueryHandler = searchQueryHandler ?? throw new ArgumentNullException(nameof(searchQueryHandler));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _promptTemplateService = promptTemplateService ?? throw new ArgumentNullException(nameof(promptTemplateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        StreamQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Issue #1445: Use centralized query validation
        var queryError = QueryValidator.ValidateQuery(query.Query);
        if (queryError != null)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError(queryError, "INVALID_QUERY"));
            yield break;
        }

        _logger.LogInformation("Starting streaming QA for game {GameId}, query: {Query}",
            query.GameId, query.Query);

        // Check cache first - if cached, return it as streaming events
        var cacheKey = _cache.GenerateQaCacheKey(query.GameId, query.Query);
        var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken);

        if (cachedResponse != null)
        {
            _logger.LogInformation("Returning cached QA response as stream for game {GameId}", query.GameId);

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
                    await Task.Delay(TimeSpan.FromMilliseconds(10), _timeProvider, cancellationToken);
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

        // Stream fresh QA response
        await foreach (var evt in AskStreamInternalAsync(query, cancellationToken))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> AskStreamInternalAsync(
        StreamQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var startTime = _timeProvider.GetUtcNow();
        var cacheKey = _cache.GenerateQaCacheKey(query.GameId, query.Query);

        // Step 1: Perform vector search
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching knowledge base..."));

        var searchQuery = new SearchQuery(
            GameId: Guid.Parse(query.GameId),
            Query: query.Query,
            TopK: 5,
            MinScore: 0.7,
            SearchMode: "hybrid",
            Language: "en"
        );

        var searchResults = await _searchQueryHandler.Handle(searchQuery, cancellationToken);

        if (searchResults.Count == 0)
        {
            _logger.LogInformation("No vector results found for query in game {GameId}", query.GameId);
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("No relevant information found in the rulebook.", "NO_RESULTS"));
            yield break;
        }

        // Map search results to domain entities for quality tracking
        var domainSearchResults = searchResults.Select(sr => new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.Parse(sr.VectorDocumentId),
            textContent: sr.TextContent,
            pageNumber: sr.PageNumber,
            relevanceScore: new Confidence(sr.RelevanceScore),
            rank: sr.Rank,
            searchMethod: sr.SearchMethod ?? "hybrid"
        )).ToList();

        // Calculate search confidence
        var searchConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

        // Step 2: Emit citations
        var snippets = searchResults.Select(r => new Snippet(
            r.TextContent,
            $"PDF:{r.VectorDocumentId}",
            r.PageNumber,
            0,
            (float)r.RelevanceScore // AI-11: Include actual search score
        )).ToList();

        yield return CreateEvent(StreamingEventType.Citations,
            new StreamingCitations(snippets));

        // Step 3: Load chat thread context if ThreadId provided (Issue #857)
        string chatHistoryContext = string.Empty;
        if (query.ThreadId.HasValue)
        {
            var thread = await _chatThreadRepository.GetByIdAsync(query.ThreadId.Value, cancellationToken);
            if (thread != null)
            {
                // Security: Validate thread belongs to requested game
                if (thread.GameId.HasValue && thread.GameId.Value.ToString() != query.GameId)
                {
                    _logger.LogWarning(
                        "Thread {ThreadId} belongs to game {ThreadGameId} but query is for game {QueryGameId}. Ignoring chat history.",
                        query.ThreadId.Value, thread.GameId.Value, query.GameId);
                }
                else if (_chatContextService.ShouldIncludeChatHistory(thread))
                {
                    chatHistoryContext = _chatContextService.BuildChatHistoryContext(thread);
                    _logger.LogInformation(
                        "Including chat history from thread {ThreadId}: {MessageCount} messages",
                        query.ThreadId.Value, thread.MessageCount);
                }
            }
        }

        // Step 4: Build LLM prompt with context
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating answer..."));

        var context = string.Join("\n\n", searchResults.Select(sr =>
            $"[Page {sr.PageNumber}] {sr.TextContent}"));

        // Use PromptTemplateService for advanced prompt engineering
        var questionType = _promptTemplateService.ClassifyQuestion(query.Query);
        Guid? gameGuid = Guid.TryParse(query.GameId, out var guid) ? guid : null;
        var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType);

        var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
        var baseUserPrompt = _promptTemplateService.RenderUserPrompt(template, context, query.Query);

        // Enrich with chat history if available
        var userPrompt = !string.IsNullOrWhiteSpace(chatHistoryContext)
            ? _chatContextService.EnrichPromptWithHistory(baseUserPrompt, chatHistoryContext)
            : baseUserPrompt;

        _logger.LogDebug(
            "Using prompt template for game {GameId}, question type {QuestionType}",
            query.GameId, questionType);

        // Step 5: Stream tokens from LLM
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

        // Calculate quality metrics
        var llmConfidence = _qualityTrackingService.CalculateLlmConfidence(answer, domainSearchResults);
        var overallConfidence = _qualityTrackingService.CalculateOverallConfidence(
            searchConfidence,
            llmConfidence);

        _logger.LogInformation(
            "Streaming QA completed for game {GameId}, {TokenCount} tokens sent, confidence: {Confidence}",
            query.GameId, tokenCount, overallConfidence.Value);

        // Build response for caching
        var response = new QaResponse(
            answer,
            snippets,
            0, // Prompt tokens not available from streaming
            tokenCount,
            tokenCount,
            overallConfidence.Value,
            null);

        // Cache the complete response
        await _cache.SetAsync(cacheKey, response, 86400, cancellationToken);

        // Emit complete event
        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(
                0, // Not applicable for QA
                0,
                tokenCount,
                tokenCount,
                overallConfidence.Value));

        // Record metrics
        var duration = (_timeProvider.GetUtcNow() - startTime).TotalMilliseconds;
        MeepleAiMetrics.RecordRagRequest(duration, query.GameId, true);
        MeepleAiMetrics.TokensUsed.Record(tokenCount,
            new System.Diagnostics.TagList { { "game.id", query.GameId }, { "operation", "qa-stream" } });
        MeepleAiMetrics.ConfidenceScore.Record(overallConfidence.Value,
            new System.Diagnostics.TagList { { "game.id", query.GameId }, { "operation", "qa-stream" } });
    }

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, _timeProvider.GetUtcNow().UtcDateTime);
    }
}
