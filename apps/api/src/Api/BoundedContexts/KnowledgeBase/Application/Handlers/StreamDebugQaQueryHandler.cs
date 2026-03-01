using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Helpers;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Admin-only handler for StreamDebugQaQuery.
/// Mirrors the StreamQaQueryHandler pipeline but interleaves debug events
/// for real-time pipeline tracing in the admin debug chat UI.
///
/// Separate handler to avoid any risk to the production streaming path.
/// </summary>
internal class StreamDebugQaQueryHandler : IStreamingQueryHandler<StreamDebugQaQuery, RagStreamingEvent>
{
    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly AgentRouterService? _agentRouterService;
    private readonly IRagValidationPipelineService? _validationPipelineService;
    private readonly ILogger<StreamDebugQaQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public StreamDebugQaQueryHandler(
        SearchQueryHandler searchQueryHandler,
        QualityTrackingDomainService qualityTrackingService,
        ChatContextDomainService chatContextService,
        IChatThreadRepository chatThreadRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        ILlmService llmService,
        IAiResponseCacheService cache,
        IPromptTemplateService promptTemplateService,
        ILogger<StreamDebugQaQueryHandler> logger,
        AgentRouterService? agentRouterService = null,
        IRagValidationPipelineService? validationPipelineService = null,
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
        _agentRouterService = agentRouterService;
        _validationPipelineService = validationPipelineService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        StreamDebugQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queryError = QueryValidator.ValidateQuery(query.Query);
        if (queryError != null)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError(queryError, "INVALID_QUERY"));
            yield break;
        }

        _logger.LogInformation("[DebugChat] Starting debug streaming QA for game {GameId}, query: {Query}, strategy override: {Strategy}",
            query.GameId, query.Query, query.StrategyOverride ?? "default");

        await foreach (var evt in DebugStreamInternalAsync(query, cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<RagStreamingEvent> DebugStreamInternalAsync(
        StreamDebugQaQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pipelineStart = Stopwatch.StartNew();

        // ── Step 1: Cache check ──────────────────────────────────────────
        var cacheStopwatch = Stopwatch.StartNew();
        var cacheKey = _cache.GenerateQaCacheKey(query.GameId, query.Query);
        var cachedResponse = await _cache.GetAsync<QaResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
        cacheStopwatch.Stop();

        yield return CreateEvent(StreamingEventType.DebugCacheCheck,
            new DebugCacheCheckData(
                Hit: cachedResponse != null,
                CacheKey: cacheKey,
                DurationMs: cacheStopwatch.Elapsed.TotalMilliseconds));

        if (cachedResponse != null)
        {
            _logger.LogInformation("[DebugChat] Cache hit for game {GameId}", query.GameId);

            yield return CreateEvent(StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Retrieved from cache"));
            yield return CreateEvent(StreamingEventType.Citations,
                new StreamingCitations(cachedResponse.snippets));

            var words = cachedResponse.answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var token = i == 0 ? words[i] : " " + words[i];
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(token));
                if (i < words.Length - 1)
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            yield return CreateEvent(StreamingEventType.Complete,
                new StreamingComplete(0, cachedResponse.promptTokens, cachedResponse.completionTokens,
                    cachedResponse.totalTokens, cachedResponse.confidence));
            yield break;
        }

        // ── Step 2: Document readiness check ─────────────────────────────
        var docStopwatch = Stopwatch.StartNew();
        var gameGuid = Guid.Parse(query.GameId);
        var documentIdsList = query.DocumentIds?.ToList();
        var (allReady, processingCount, totalCount) = await CheckDocumentsReadyAsync(
            gameGuid, documentIdsList, cancellationToken).ConfigureAwait(false);
        docStopwatch.Stop();

        yield return CreateEvent(StreamingEventType.DebugDocumentCheck,
            new DebugDocumentCheckData(
                AllReady: allReady,
                ProcessingCount: processingCount,
                TotalCount: totalCount,
                DurationMs: docStopwatch.Elapsed.TotalMilliseconds));

        if (!allReady)
        {
            yield return CreateEvent(StreamingEventType.Error, new StreamingError(
                $"{processingCount} of {totalCount} documents are still being processed. Please wait for processing to complete before asking questions.",
                "DOCUMENT_PROCESSING"));
            yield break;
        }

        // ── Step 3: Agent routing (if available) ─────────────────────────
        if (_agentRouterService != null)
        {
            RagStreamingEvent? routingEvent = null;
            try
            {
                var routingDecision = _agentRouterService.RouteQuery(query.Query);
                routingEvent = CreateEvent(StreamingEventType.DebugAgentRouter,
                    new DebugAgentRouterData(
                        TargetAgent: routingDecision.TargetAgent,
                        Intent: routingDecision.Intent.ToString(),
                        Confidence: routingDecision.Confidence));
            }
#pragma warning disable CA1031 // Agent routing is optional debug info
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DebugChat] Agent router failed, continuing without routing info");
            }
#pragma warning restore CA1031
            if (routingEvent != null) yield return routingEvent;
        }

        // ── Step 4: Strategy selection ───────────────────────────────────
        var strategyName = query.StrategyOverride ?? "HybridSearch";
        yield return CreateEvent(StreamingEventType.DebugStrategySelected,
            new DebugStrategySelectedData(
                StrategyName: strategyName,
                Parameters: new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["topK"] = 5,
                    ["minScore"] = 0.55,
                    ["searchMode"] = "hybrid"
                },
                OverrideSource: query.StrategyOverride != null ? "user" : null));

        // ── Step 5: Retrieval ────────────────────────────────────────────
        yield return CreateEvent(StreamingEventType.DebugRetrievalStart,
            new DebugRetrievalStartData(
                SearchMode: "hybrid",
                TopK: 5,
                MinScore: 0.55));

        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Searching knowledge base..."));

        var searchStopwatch = Stopwatch.StartNew();
        var (searchSuccess, snippets, domainSearchResults, searchConfidence) = await PerformSearchAndBuildCitationsAsync(
            query.GameId, query.Query, query.DocumentIds, cancellationToken).ConfigureAwait(false);
        searchStopwatch.Stop();

        // ── Step 6: Search details ───────────────────────────────────────
        yield return CreateEvent(StreamingEventType.DebugSearchDetails,
            new DebugSearchDetailsData(
                VectorResultCount: snippets?.Count ?? 0,
                KeywordResultCount: 0,
                FusedResultCount: snippets?.Count ?? 0,
                VectorDurationMs: searchStopwatch.Elapsed.TotalMilliseconds,
                KeywordDurationMs: 0));

        if (!searchSuccess)
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("No relevant information found in the rulebook.", "NO_RESULTS"));
            yield break;
        }

        // ── Step 7: Retrieval results ────────────────────────────────────
        var retrievalItems = snippets!.Select(s => new DebugRetrievalItem(
            DocumentId: s.source,
            Score: s.score,
            PageNumber: s.page,
            SearchMethod: "hybrid")).ToList();

        yield return CreateEvent(StreamingEventType.DebugRetrievalResults,
            new DebugRetrievalResultsData(
                Count: snippets!.Count,
                Items: retrievalItems,
                DurationMs: searchStopwatch.Elapsed.TotalMilliseconds));

        yield return CreateEvent(StreamingEventType.Citations,
            new StreamingCitations(snippets));

        // ── Step 8: Chat context ─────────────────────────────────────────
        var chatHistoryContext = await LoadChatThreadContextAsync(
            query.ThreadId, query.GameId, cancellationToken).ConfigureAwait(false);

        // ── Step 9: Build prompts ────────────────────────────────────────
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Generating answer..."));

        var (systemPrompt, userPrompt) = await BuildLlmPromptsAsync(
            query.GameId, query.Query, snippets, chatHistoryContext).ConfigureAwait(false);

        var estimatedTokens = (systemPrompt.Length + userPrompt.Length) / 4; // rough estimate
        if (query.IncludePrompts)
        {
            yield return CreateEvent(StreamingEventType.DebugPromptContext,
                new DebugPromptContextData(
                    SystemPrompt: systemPrompt,
                    UserPrompt: userPrompt,
                    EstimatedTokens: estimatedTokens));
        }
        else
        {
            yield return CreateEvent(StreamingEventType.DebugPromptContext,
                new DebugPromptContextData(
                    SystemPrompt: "[hidden — enable includePrompts to view]",
                    UserPrompt: "[hidden — enable includePrompts to view]",
                    EstimatedTokens: estimatedTokens));
        }

        // ── Step 10: Stream LLM tokens ───────────────────────────────────
        var answerBuilder = new StringBuilder();
        var tokenCount = 0;
        LlmUsage? llmUsage = null;
        LlmCost? llmCost = null;

        await foreach (var chunk in _llmService.GenerateCompletionStreamAsync(systemPrompt, userPrompt, RequestSource.AdminOperation, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (chunk.IsFinal && chunk.Usage != null && chunk.Cost != null)
            {
                llmUsage = chunk.Usage;
                llmCost = chunk.Cost;
                continue;
            }

            if (!string.IsNullOrEmpty(chunk.Content))
            {
                answerBuilder.Append(chunk.Content);
                tokenCount++;
                yield return CreateEvent(StreamingEventType.Token, new StreamingToken(chunk.Content));
            }
        }

        var answer = answerBuilder.ToString().Trim();

        // ── Step 11: Cost update ─────────────────────────────────────────
        yield return CreateEvent(StreamingEventType.DebugCostUpdate,
            new DebugCostUpdateData(
                PromptTokens: llmUsage?.PromptTokens ?? 0,
                CompletionTokens: llmUsage?.CompletionTokens ?? tokenCount,
                TotalTokens: llmUsage?.TotalTokens ?? tokenCount,
                CostUsd: llmCost != null ? (double)llmCost.TotalCost : null,
                ModelId: llmCost?.ModelId));

        // ── Step 12: Quality + cache ─────────────────────────────────────
        var overallConfidence = await CalculateAndCacheResponseAsync(
            answer, snippets, domainSearchResults!, searchConfidence ?? Confidence.Parse(0.5),
            tokenCount, cacheKey, llmUsage, llmCost, cancellationToken).ConfigureAwait(false);

        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(0, llmUsage?.PromptTokens ?? 0, tokenCount, llmUsage?.TotalTokens ?? tokenCount, overallConfidence.Value));

        // ── Step 13: Validation pipeline (async, best-effort) ────────────
        if (_validationPipelineService != null)
        {
            var validationEvents = await RunValidationPipelineAsync(
                answer, snippets, tokenCount, overallConfidence.Value, query.GameId, cancellationToken).ConfigureAwait(false);
            foreach (var validationEvent in validationEvents)
            {
                yield return validationEvent;
            }
        }

        pipelineStart.Stop();
        _logger.LogInformation("[DebugChat] Debug pipeline completed in {DurationMs:F1}ms for game {GameId}",
            pipelineStart.Elapsed.TotalMilliseconds, query.GameId);
    }

    /// <summary>
    /// Runs the validation pipeline and collects events.
    /// Extracted to avoid yield-in-try-catch limitation.
    /// </summary>
    private async Task<List<RagStreamingEvent>> RunValidationPipelineAsync(
        string answer, List<Snippet> snippets, int tokenCount, double confidence,
        string gameId, CancellationToken cancellationToken)
    {
        var events = new List<RagStreamingEvent>();
        var validationStopwatch = Stopwatch.StartNew();

        try
        {
            var qaResponse = new QaResponse(answer, snippets, 0, tokenCount, tokenCount, confidence);
            var validationResult = await _validationPipelineService!.ValidateResponseAsync(
                qaResponse, gameId, cancellationToken: cancellationToken).ConfigureAwait(false);
            validationStopwatch.Stop();

            events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                new DebugValidationLayerData(1, "Confidence", validationResult.ConfidenceValidation.IsValid,
                    validationStopwatch.Elapsed.TotalMilliseconds)));

            events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                new DebugValidationLayerData(3, "Citation", validationResult.CitationValidation.IsValid,
                    validationStopwatch.Elapsed.TotalMilliseconds)));

            events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                new DebugValidationLayerData(4, "Hallucination", validationResult.HallucinationDetection.IsValid,
                    validationStopwatch.Elapsed.TotalMilliseconds)));

            if (validationResult.MultiModelConsensus != null)
            {
                events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                    new DebugValidationLayerData(2, "MultiModel", validationResult.MultiModelConsensus.HasConsensus,
                        validationStopwatch.Elapsed.TotalMilliseconds)));
            }
        }
#pragma warning disable CA1031 // Validation is optional debug info
        catch (Exception ex)
        {
            validationStopwatch.Stop();
            _logger.LogWarning(ex, "[DebugChat] Validation pipeline failed");
            events.Add(CreateEvent(StreamingEventType.DebugValidationLayer,
                new DebugValidationLayerData(0, "Pipeline", false,
                    validationStopwatch.Elapsed.TotalMilliseconds,
                    Details: ex.Message)));
        }
#pragma warning restore CA1031

        return events;
    }

    // ── Private helpers (same logic as StreamQaQueryHandler) ─────────────

    private async Task<(bool success, List<Snippet>? snippets, List<Domain.Entities.SearchResult>? domainResults, Confidence? searchConfidence)> PerformSearchAndBuildCitationsAsync(
        string gameId, string queryText, IReadOnlyList<Guid>? documentIds, CancellationToken cancellationToken)
    {
        var searchQuery = new SearchQuery(
            GameId: Guid.Parse(gameId),
            Query: queryText,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "hybrid",
            Language: "en",
            DocumentIds: documentIds);

        var searchResults = await _searchQueryHandler.Handle(searchQuery, cancellationToken).ConfigureAwait(false);

        if (searchResults == null || searchResults.Count == 0)
        {
            _logger.LogInformation("[DebugChat] No vector results found for query in game {GameId}", gameId);
            return (false, null, null, null);
        }

        var domainSearchResults = searchResults.Select(sr => new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.Parse(sr.VectorDocumentId),
            textContent: sr.TextContent,
            pageNumber: Math.Max(1, sr.PageNumber),
            relevanceScore: new Confidence(sr.RelevanceScore),
            rank: sr.Rank,
            searchMethod: sr.SearchMethod ?? "hybrid"
        )).ToList();

        var searchConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

        var snippets = searchResults.Select(r => new Snippet(
            r.TextContent, $"PDF:{r.VectorDocumentId}", r.PageNumber, 0, (float)r.RelevanceScore
        )).ToList();

        return (true, snippets, domainSearchResults, searchConfidence);
    }

    private async Task<string> LoadChatThreadContextAsync(
        Guid? threadId, string gameId, CancellationToken cancellationToken)
    {
        if (!threadId.HasValue)
            return string.Empty;

        var thread = await _chatThreadRepository.GetByIdAsync(threadId.Value, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            return string.Empty;

        if (thread.GameId.HasValue && !string.Equals(thread.GameId.Value.ToString(), gameId, StringComparison.Ordinal))
        {
            _logger.LogWarning("[DebugChat] Thread {ThreadId} belongs to different game, ignoring history", threadId.Value);
            return string.Empty;
        }

        if (_chatContextService.ShouldIncludeChatHistory(thread))
            return _chatContextService.BuildChatHistoryContext(thread);

        return string.Empty;
    }

    private async Task<(string systemPrompt, string userPrompt)> BuildLlmPromptsAsync(
        string gameId, string queryText, List<Snippet> snippets, string chatHistoryContext)
    {
        var context = string.Join("\n\n", snippets.Select(s => $"[Page {s.page}] {s.text}"));

        var questionType = _promptTemplateService.ClassifyQuestion(queryText);
        Guid? gameGuid = Guid.TryParse(gameId, out var guid) ? guid : null;
        var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType).ConfigureAwait(false);

        var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
        var baseUserPrompt = _promptTemplateService.RenderUserPrompt(template, context, queryText);

        var userPrompt = !string.IsNullOrWhiteSpace(chatHistoryContext)
            ? _chatContextService.EnrichPromptWithHistory(baseUserPrompt, chatHistoryContext)
            : baseUserPrompt;

        return (systemPrompt, userPrompt);
    }

    private Task<Confidence> CalculateAndCacheResponseAsync(
        string answer, List<Snippet> snippets, List<Domain.Entities.SearchResult> domainSearchResults,
        Confidence searchConfidence, int tokenCount, string cacheKey,
        LlmUsage? llmUsage, LlmCost? llmCost, CancellationToken cancellationToken)
    {
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

        var llmConfidence = _qualityTrackingService.CalculateLlmConfidence(answer, domainSearchResults);
        var overallConfidence = _qualityTrackingService.CalculateOverallConfidence(searchConfidence, llmConfidence);

        // Debug handler intentionally skips cache writes to avoid polluting
        // the production cache with strategy-override or debug-session results.

        return Task.FromResult(overallConfidence);
    }

    private async Task<(bool allReady, int processing, int total)> CheckDocumentsReadyAsync(
        Guid gameId, List<Guid>? documentIds, CancellationToken ct)
    {
        var documents = documentIds?.Count > 0
            ? await _pdfDocumentRepository.GetByIdsAsync(documentIds, ct).ConfigureAwait(false)
            : await _pdfDocumentRepository.FindByGameIdAsync(gameId, ct).ConfigureAwait(false);

        if (documents is null or { Count: 0 }) return (true, 0, 0);

        var notCompleted = documents.Count(d =>
            !string.Equals(d.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase));
        return (notCompleted == 0, notCompleted, documents.Count);
    }

    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, _timeProvider.GetUtcNow().UtcDateTime);
    }
}
