using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
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
/// Implements streaming RAG Q&amp;A with token-by-token delivery and chat context integration.
/// CHAT-01: Integrates with ChatContextDomainService for conversation continuity
/// AI-11: Tracks quality metrics and confidence scoring
/// </summary>
internal class StreamQaQueryHandler : IStreamingQueryHandler<StreamQaQuery, RagStreamingEvent>
{
    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
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
        IPdfDocumentRepository pdfDocumentRepository,
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
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
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
        var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken).ConfigureAwait(false);

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

                // Small delay to simulate streaming (use Task.Delay directly to avoid FakeTimeProvider issues in tests)
                if (i < words.Length - 1)
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
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
        await foreach (var evt in AskStreamInternalAsync(query, cancellationToken).ConfigureAwait(false))
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

        // Step 0: Check if documents are still being processed
        var gameGuid = Guid.Parse(query.GameId);
        var documentIdsList = query.DocumentIds?.ToList();
        var (allReady, processingCount, totalCount) = await CheckDocumentsReadyAsync(
            gameGuid, documentIdsList, cancellationToken).ConfigureAwait(false);

        if (!allReady)
        {
            _logger.LogInformation(
                "Documents not ready for game {GameId}: {Processing}/{Total} still processing",
                query.GameId, processingCount, totalCount);
            yield return CreateEvent(StreamingEventType.Error, new StreamingError(
                $"{processingCount} of {totalCount} documents are still being processed. Please wait for processing to complete before asking questions.",
                "DOCUMENT_PROCESSING"));
            yield break;
        }

        // Step 1: Perform search and build citations
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching knowledge base..."));

        var (searchSuccess, snippets, domainSearchResults, searchConfidence) = await PerformSearchAndBuildCitationsAsync(
            query.GameId, query.Query, query.DocumentIds, cancellationToken).ConfigureAwait(false);

        if (!searchSuccess)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("No relevant information found in the rulebook.", "NO_RESULTS"));
            yield break;
        }

        yield return CreateEvent(StreamingEventType.Citations,
            new StreamingCitations(snippets!));

        // Step 2: Load chat thread context if ThreadId provided
        var chatHistoryContext = await LoadChatThreadContextAsync(
            query.ThreadId, query.GameId, cancellationToken).ConfigureAwait(false);

        // Step 3: Build LLM prompts with context
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating answer..."));

        var (systemPrompt, userPrompt) = await BuildLlmPromptsAsync(
            query.GameId, query.Query, snippets!, chatHistoryContext).ConfigureAwait(false);

        // Step 4: Stream tokens from LLM
        var answerBuilder = new StringBuilder();
        var tokenCount = 0;
        LlmUsage? llmUsage = null;
        LlmCost? llmCost = null;

        await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(systemPrompt, userPrompt, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ISSUE-1725: Handle final chunk with usage metadata
            if (chunk.IsFinal && chunk.Usage != null && chunk.Cost != null)
            {
                llmUsage = chunk.Usage;
                llmCost = chunk.Cost;
                _logger.LogInformation(
                    "Streaming usage received: {PromptTokens}p + {CompletionTokens}c = {TotalTokens}t (${Cost:F6})",
                    llmUsage.PromptTokens, llmUsage.CompletionTokens, llmUsage.TotalTokens, llmCost.TotalCost);
                continue;
            }

            // Regular content chunk
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                answerBuilder.Append(chunk.Content);
                tokenCount++;
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(chunk.Content));
            }
        }

        var answer = answerBuilder.ToString().Trim();

        // Step 5: Calculate quality metrics, cache response, emit completion
        var overallConfidence = await CalculateAndCacheResponseAsync(
            answer, snippets!, domainSearchResults!, searchConfidence ?? Confidence.Parse(0.5),
            tokenCount, cacheKey, llmUsage, llmCost, cancellationToken).ConfigureAwait(false);

        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(0, 0, tokenCount, tokenCount, overallConfidence.Value));

        // Record metrics
        var duration = (_timeProvider.GetUtcNow() - startTime).TotalMilliseconds;
        MeepleAiMetrics.RecordRagRequest(duration, query.GameId, true);
        MeepleAiMetrics.TokensUsed.Record(tokenCount,
            new System.Diagnostics.TagList { { "game.id", query.GameId }, { "operation", "qa-stream" } });
        MeepleAiMetrics.ConfidenceScore.Record(overallConfidence.Value,
            new System.Diagnostics.TagList { { "game.id", query.GameId }, { "operation", "qa-stream" } });
    }

    /// <summary>
    /// Performs vector search and builds citations from results.
    /// Returns (success, snippets, domainResults, confidence).
    /// </summary>
    private async Task<(bool success, List<Snippet>? snippets, List<Domain.Entities.SearchResult>? domainResults, Confidence? searchConfidence)> PerformSearchAndBuildCitationsAsync(
        string gameId,
        string queryText,
        IReadOnlyList<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        var searchQuery = new SearchQuery(
            GameId: Guid.Parse(gameId),
            Query: queryText,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "hybrid",
            Language: "en",
            DocumentIds: documentIds // Issue #2051
        );

        var searchResults = await _searchQueryHandler.Handle(searchQuery, cancellationToken).ConfigureAwait(false);

        if (searchResults == null || searchResults.Count == 0)
        {
            _logger.LogInformation("No vector results found for query in game {GameId}", gameId);
            return (false, null, null, null);
        }

        // Map search results to domain entities for quality tracking
        var domainSearchResults = searchResults.Select(sr => new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.Parse(sr.VectorDocumentId),
            textContent: sr.TextContent,
            pageNumber: Math.Max(1, sr.PageNumber),
            relevanceScore: new Confidence(sr.RelevanceScore),
            rank: sr.Rank,
            searchMethod: sr.SearchMethod ?? "hybrid"
        )).ToList();

        // Calculate search confidence
        var searchConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

        // Build citations
        var snippets = searchResults.Select(r => new Snippet(
            r.TextContent,
            $"PDF:{r.VectorDocumentId}",
            r.PageNumber,
            0,
            (float)r.RelevanceScore
        )).ToList();

        return (true, snippets, domainSearchResults, searchConfidence);
    }

    /// <summary>
    /// Loads chat thread context if ThreadId provided.
    /// </summary>
    private async Task<string> LoadChatThreadContextAsync(
        Guid? threadId,
        string gameId,
        CancellationToken cancellationToken)
    {
        if (!threadId.HasValue)
            return string.Empty;

        var thread = await _chatThreadRepository.GetByIdAsync(threadId.Value, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            return string.Empty;

        // Security: Validate thread belongs to requested game
        if (thread.GameId.HasValue && !string.Equals(thread.GameId.Value.ToString(), gameId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Thread {ThreadId} belongs to game {ThreadGameId} but query is for game {QueryGameId}. Ignoring chat history.",
                threadId.Value, thread.GameId.Value, gameId);
            return string.Empty;
        }

        if (_chatContextService.ShouldIncludeChatHistory(thread))
        {
            _logger.LogInformation(
                "Including chat history from thread {ThreadId}: {MessageCount} messages",
                threadId.Value, thread.MessageCount);
            return _chatContextService.BuildChatHistoryContext(thread);
        }

        return string.Empty;
    }

    /// <summary>
    /// Builds LLM prompts using templates and chat history enrichment.
    /// Returns (systemPrompt, userPrompt).
    /// </summary>
    private async Task<(string systemPrompt, string userPrompt)> BuildLlmPromptsAsync(
        string gameId,
        string queryText,
        List<Snippet> snippets,
        string chatHistoryContext
        )

    {
        var context = string.Join("\n\n", snippets.Select(s =>
            $"[Page {s.page}] {s.text}"));

        // Use PromptTemplateService for advanced prompt engineering
        var questionType = _promptTemplateService.ClassifyQuestion(queryText);
        Guid? gameGuid = Guid.TryParse(gameId, out var guid) ? guid : null;
        var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType).ConfigureAwait(false);

        var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
        var baseUserPrompt = _promptTemplateService.RenderUserPrompt(template, context, queryText);

        // Enrich with chat history if available
        var userPrompt = !string.IsNullOrWhiteSpace(chatHistoryContext)
            ? _chatContextService.EnrichPromptWithHistory(baseUserPrompt, chatHistoryContext)
            : baseUserPrompt;

        _logger.LogDebug(
            "Using prompt template for game {GameId}, question type {QuestionType}",
            gameId, questionType);

        return (systemPrompt, userPrompt);
    }

    /// <summary>
    /// Calculates quality metrics, caches response, and records LLM usage.
    /// </summary>
    private async Task<Confidence> CalculateAndCacheResponseAsync(
        string answer,
        List<Snippet> snippets,
        List<Domain.Entities.SearchResult> domainSearchResults,
        Confidence searchConfidence,
        int tokenCount,
        string cacheKey,
        LlmUsage? llmUsage,
        LlmCost? llmCost,
        CancellationToken cancellationToken)
    {
        // ISSUE-1725: Record LLM token usage with OpenTelemetry GenAI semantic conventions
        if (llmUsage != null && llmCost != null)
        {
            Api.Observability.MeepleAiMetrics.RecordLlmTokenUsage(
                promptTokens: llmUsage.PromptTokens,
                completionTokens: llmUsage.CompletionTokens,
                totalTokens: llmUsage.TotalTokens,
                modelId: llmCost.ModelId,
                provider: llmCost.Provider,
                operationDurationMs: null,
                costUsd: llmCost.TotalCost);
        }

        // Calculate quality metrics
        var llmConfidence = _qualityTrackingService.CalculateLlmConfidence(answer, domainSearchResults);
        var overallConfidence = _qualityTrackingService.CalculateOverallConfidence(
            searchConfidence,
            llmConfidence);

        // Build and cache response
        var response = new QaResponse(
            answer,
            snippets,
            0,
            tokenCount,
            tokenCount,
            overallConfidence.Value,
            null);

        await _cache.SetAsync(cacheKey, response, 86400, cancellationToken).ConfigureAwait(false);

        return overallConfidence;
    }

    private async Task<(bool allReady, int processing, int total)> CheckDocumentsReadyAsync(
        Guid gameId, List<Guid>? documentIds, CancellationToken ct)
    {
        var documents = documentIds?.Count > 0
            ? await _pdfDocumentRepository.GetByIdsAsync(documentIds, ct).ConfigureAwait(false)
            : await _pdfDocumentRepository.FindByGameIdAsync(gameId, ct).ConfigureAwait(false);

        if (documents.Count == 0) return (true, 0, 0);

        var notCompleted = documents.Count(d =>
            !string.Equals(d.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase));
        return (notCompleted == 0, notCompleted, documents.Count);
    }

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, _timeProvider.GetUtcNow().UtcDateTime);
    }
}
