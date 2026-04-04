using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for AskQuestionQuery.
/// Orchestrates RAG (Retrieval-Augmented Generation) flow:
/// 1. Vector search for relevant context
/// 2. LLM generation with context
/// 3. Quality tracking and confidence scoring
/// </summary>
internal class AskQuestionQueryHandler : IQueryHandler<AskQuestionQuery, QaResponseDto>
{
    private const string DefaultSystemPrompt =
        "You are MeepleAI, a precise board game rules assistant. " +
        "Answer ONLY using the provided rulebook context. " +
        "For each claim you make, it MUST be directly supported by the context provided. " +
        "If the context does not contain the answer, respond EXACTLY with: " +
        "'This information is not available in the provided rulebook.' " +
        "Never invent rules, game mechanics, or examples not present in the context. " +
        "Always cite the page number in brackets, e.g. [Page 3].";

    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly ILlmService _llmService;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly IRagValidationPipelineService _validationPipeline;
    private readonly IRagAccessService _ragAccessService;
    private readonly IRagQualityTracker _qualityTracker;
    private readonly QueryComplexityAnalyzer _complexityAnalyzer;
    private readonly ISemanticResponseCache _responseCache;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<AskQuestionQueryHandler> _logger;

    public AskQuestionQueryHandler(
        SearchQueryHandler searchQueryHandler,
        QualityTrackingDomainService qualityTrackingService,
        ChatContextDomainService chatContextService,
        IChatThreadRepository chatThreadRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        ILlmService llmService,
        IPromptTemplateService promptTemplateService,
        IRagValidationPipelineService validationPipeline,
        IRagAccessService ragAccessService,
        IRagQualityTracker qualityTracker,
        QueryComplexityAnalyzer complexityAnalyzer,
        ISemanticResponseCache responseCache,
        IEmbeddingService embeddingService,
        ILogger<AskQuestionQueryHandler> logger)
    {
        _searchQueryHandler = searchQueryHandler ?? throw new ArgumentNullException(nameof(searchQueryHandler));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _promptTemplateService = promptTemplateService ?? throw new ArgumentNullException(nameof(promptTemplateService));
        _validationPipeline = validationPipeline ?? throw new ArgumentNullException(nameof(validationPipeline));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _qualityTracker = qualityTracker ?? throw new ArgumentNullException(nameof(qualityTracker));
        _complexityAnalyzer = complexityAnalyzer ?? throw new ArgumentNullException(nameof(complexityAnalyzer));
        _responseCache = responseCache ?? throw new ArgumentNullException(nameof(responseCache));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QaResponseDto> Handle(
        AskQuestionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var startTime = DateTime.UtcNow;

        // P1-4: Analyze query complexity for intelligent LLM model routing (~40% cost reduction)
        var queryRoutingTier = _complexityAnalyzer.Analyze(query.Question);
        _logger.LogDebug(
            "[AskQuestionHandler] QueryComplexityAnalyzer: Question={Question}, RoutingTier={RoutingTier}",
            query.Question, queryRoutingTier);
#pragma warning disable S1135, MA0026 // Deferred: requires ILlmService per-call model-override support (not yet available)
        // TODO: Pass queryRoutingTier to ILlmService for model selection when per-call model-override support is available.
        // Currently the tier is captured in RagQueryMetrics.Strategy for observability only.
#pragma warning restore S1135, MA0026

        // RAG access enforcement
        if (query.UserId.HasValue)
        {
            var userRole = Enum.TryParse<UserRole>(query.UserRole, ignoreCase: true, out var parsedRole)
                ? parsedRole : UserRole.User;
            var canAccess = await _ragAccessService.CanAccessRagAsync(
                query.UserId.Value, query.GameId, userRole, cancellationToken).ConfigureAwait(false);
            if (!canAccess)
                throw new ForbiddenException("Accesso RAG non autorizzato");
        }

        _logger.LogInformation(
            "[AskQuestionHandler] ENTRY - Processing AskQuestionQuery: GameId={GameId}, Question={Question}",
            query.GameId, query.Question);

        // P1-5: Semantic cache lookup — generate query vector and check for a cached response
        float[]? queryVector = null;
        if (!query.BypassCache)
        {
            try
            {
                var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
                    query.Question, query.Language, cancellationToken).ConfigureAwait(false);
                if (embeddingResult.Success && embeddingResult.Embeddings.Count > 0)
                {
                    queryVector = embeddingResult.ToFloatArray();
                    var cached = await _responseCache.TryGetAsync(
                        query.GameId, queryVector, cancellationToken).ConfigureAwait(false);
                    if (cached != null)
                    {
                        _logger.LogInformation(
                            "[AskQuestionHandler] Cache hit for game {GameId} — serving cached response",
                            query.GameId);
                        var cacheMetrics = new RagQueryMetrics(
                            ThreadId: query.ThreadId,
                            GameId: query.GameId,
                            QueryLength: query.Question.Length,
                            ChunksRetrieved: 0,
                            ChunksUsed: 0,
                            CitationsCount: cached.Citations.Count,
                            Strategy: $"cache|tier:{queryRoutingTier}",
                            ModelUsed: cached.ModelUsed,
                            LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                            CacheHit: true,
                            NoRelevantContext: false);
                        await _qualityTracker.TrackQueryAsync(cacheMetrics, cancellationToken)
                            .ConfigureAwait(false);
                        return new QaResponseDto(
                            Answer: cached.Answer,
                            Sources: [],
                            SearchConfidence: 0,
                            LlmConfidence: 0,
                            OverallConfidence: 0,
                            IsLowQuality: false,
                            Citations: cached.Citations
                                .Select((c, i) => new CitationDto(
                                    DocumentId: string.Empty,
                                    PageNumber: i + 1,
                                    Snippet: c,
                                    RelevanceScore: 0))
                                .ToList());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[AskQuestionHandler] Semantic cache check failed for game {GameId} — proceeding without cache",
                    query.GameId);
            }
        }

        // Step 0: Check if documents are still being processed
        var (allReady, processingCount, totalCount) = await CheckDocumentsReadyAsync(
            query.GameId, null, cancellationToken).ConfigureAwait(false);

        if (!allReady)
        {
            _logger.LogInformation(
                "Documents not ready for game {GameId}: {Processing}/{Total} still processing",
                query.GameId, processingCount, totalCount);
            return new QaResponseDto(
                Answer: $"{processingCount} of {totalCount} documents are still being processed. Please wait for processing to complete before asking questions.",
                Sources: [],
                SearchConfidence: 0,
                LlmConfidence: 0,
                OverallConfidence: 0,
                IsLowQuality: false,
                Citations: [],
                ErrorCode: "DOCUMENT_PROCESSING");
        }

        // Step 1: Perform vector search and calculate confidence
        _logger.LogDebug("[AskQuestionHandler] Step 1: Starting vector search...");
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var (searchResults, domainSearchResults, searchConfidence) = await PerformSearchAndCalculateConfidenceAsync(
            query, cancellationToken).ConfigureAwait(false);
        sw1.Stop();
        _logger.LogInformation("[AskQuestionHandler] Step 1 DONE: Vector search completed in {ElapsedMs}ms - {ResultCount} results, confidence: {Confidence}",
            sw1.ElapsedMilliseconds, searchResults.Count, searchConfidence.Value);

        // No-context early exit: skip LLM call when vector search returned no results
        if (searchResults.Count == 0)
        {
            _logger.LogInformation("[AskQuestionHandler] No search results found, returning early without LLM call");
            var noContextMetrics = new RagQueryMetrics(
                ThreadId: query.ThreadId,
                GameId: query.GameId,
                QueryLength: query.Question.Length,
                ChunksRetrieved: 0,
                ChunksUsed: 0,
                CitationsCount: 0,
                Strategy: $"{query.SearchMode ?? "hybrid"}|tier:{queryRoutingTier}",
                ModelUsed: "none",
                LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                CacheHit: false,
                NoRelevantContext: true);
            await _qualityTracker.TrackQueryAsync(noContextMetrics, cancellationToken).ConfigureAwait(false);

            return new QaResponseDto(
                Answer: "This information is not available in the provided rulebook.",
                Sources: [],
                SearchConfidence: 0,
                LlmConfidence: 0,
                OverallConfidence: 0,
                IsLowQuality: true,
                Citations: []);
        }

        // Step 2: Load chat thread context if ThreadId provided
        _logger.LogDebug("[AskQuestionHandler] Step 2: Loading chat history context...");
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var chatHistoryContext = await LoadChatHistoryContextAsync(
            query.ThreadId, query.GameId, cancellationToken).ConfigureAwait(false);
        sw2.Stop();
        _logger.LogInformation("[AskQuestionHandler] Step 2 DONE: Chat context loaded in {ElapsedMs}ms - {ContextLength} chars",
            sw2.ElapsedMilliseconds, chatHistoryContext?.Length ?? 0);

        // Step 3: Build LLM prompts
        _logger.LogDebug("[AskQuestionHandler] Step 3: Building LLM prompts...");
        var systemPrompt = await _promptTemplateService.GetActivePromptAsync("rag-system-prompt")
            .ConfigureAwait(false) ?? DefaultSystemPrompt;
        var context = string.Join("\n\n", searchResults.Select(sr =>
            $"[Page {sr.PageNumber}] {sr.TextContent}"));

        var baseQuestion = $"Question: {query.Question}\n\nContext:\n{context}";
        var userPrompt = !string.IsNullOrWhiteSpace(chatHistoryContext)
            ? _chatContextService.EnrichPromptWithHistory(baseQuestion, chatHistoryContext)
            : baseQuestion;
        _logger.LogDebug("[AskQuestionHandler] Step 3 DONE: Prompts built - System: {SysLen} chars, User: {UserLen} chars",
            systemPrompt.Length, userPrompt.Length);

        // Step 4: Generate answer with LLM and record metrics
        _logger.LogDebug("[AskQuestionHandler] Step 4: Generating LLM answer...");
        var sw4 = System.Diagnostics.Stopwatch.StartNew();
        var (llmResponse, llmResult) = await GenerateLlmAnswerAndRecordMetricsAsync(
            systemPrompt, userPrompt, cancellationToken).ConfigureAwait(false);
        sw4.Stop();
        _logger.LogInformation("[AskQuestionHandler] Step 4 DONE: LLM generation completed in {ElapsedMs}ms - Response: {ResponseLength} chars",
            sw4.ElapsedMilliseconds, llmResponse?.Length ?? 0);

        // Step 5: Build validated response with quality metrics and citations
        _logger.LogDebug("[AskQuestionHandler] Step 5: Building validated response...");
        var sw5 = System.Diagnostics.Stopwatch.StartNew();
        var response = await BuildValidatedResponseAsync(
            query, llmResponse ?? string.Empty, searchResults, domainSearchResults,
            searchConfidence, systemPrompt, userPrompt, cancellationToken).ConfigureAwait(false);
        sw5.Stop();
        _logger.LogInformation("[AskQuestionHandler] Step 5 DONE: Response validation completed in {ElapsedMs}ms",
            sw5.ElapsedMilliseconds);

        _logger.LogInformation(
            "[AskQuestionHandler] COMPLETE - AskQuestionQuery completed: OverallConfidence={Confidence}, IsLowQuality={IsLowQuality}",
            response.OverallConfidence, response.IsLowQuality);

        // P1-5: Store successful response in semantic cache for future queries
        if (!query.BypassCache && queryVector != null)
        {
            var citationTexts = response.Citations?
                .Select(c => c.Snippet)
                .ToList() ?? [];
            await _responseCache.SetAsync(
                query.GameId,
                queryVector,
                new CachedRagResponse(
                    response.Answer,
                    citationTexts,
                    llmResult.Cost.ModelId ?? "unknown",
                    DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);
        }

        var successMetrics = new RagQueryMetrics(
            ThreadId: query.ThreadId,
            GameId: query.GameId,
            QueryLength: query.Question.Length,
            ChunksRetrieved: searchResults.Count,
            ChunksUsed: searchResults.Count, // currently equal to ChunksRetrieved — all retrieved chunks are passed to LLM context
            CitationsCount: response.Citations?.Count ?? 0,
            Strategy: $"{query.SearchMode ?? "hybrid"}|tier:{queryRoutingTier}",
            ModelUsed: llmResult.Cost.ModelId ?? "unknown",
            LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
            CacheHit: false,
            NoRelevantContext: false);
        await _qualityTracker.TrackQueryAsync(successMetrics, cancellationToken).ConfigureAwait(false);

        return response;
    }

    /// <summary>
    /// Performs vector search and calculates search confidence.
    /// Returns (searchResults, domainSearchResults, searchConfidence).
    /// </summary>
    private async Task<(List<SearchResultDto> searchResults, List<Domain.Entities.SearchResult> domainResults, Confidence confidence)> PerformSearchAndCalculateConfidenceAsync(
        AskQuestionQuery query,
        CancellationToken cancellationToken)
    {
        var searchQuery = new SearchQuery(
            GameId: query.GameId,
            Query: query.Question,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: query.SearchMode ?? "hybrid",
            Language: query.Language
        );

        var searchResults = await _searchQueryHandler.Handle(searchQuery, cancellationToken).ConfigureAwait(false);

        // Map search results to domain entities for quality tracking
        var domainSearchResults = searchResults.Select(sr => new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.Parse(sr.VectorDocumentId),
            textContent: sr.TextContent,
            pageNumber: Math.Max(1, sr.PageNumber),
            relevanceScore: new Confidence(sr.RelevanceScore),
            rank: sr.Rank,
            searchMethod: sr.SearchMethod ?? "unknown"
        )).ToList();

        // Calculate search confidence
        var searchConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

        return (searchResults, domainSearchResults, searchConfidence);
    }

    /// <summary>
    /// Loads chat history context if ThreadId provided with security validation.
    /// </summary>
    private async Task<string> LoadChatHistoryContextAsync(
        Guid? threadId,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        if (!threadId.HasValue)
            return string.Empty;

        var thread = await _chatThreadRepository.GetByIdAsync(threadId.Value, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            return string.Empty;

        // Security: Validate thread belongs to requested game (prevent cross-game data leak)
        if (thread.GameId.HasValue && thread.GameId.Value != gameId)
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
    /// Generates LLM answer and records token usage metrics.
    /// Returns (llmResponse, llmResult).
    /// </summary>
    private async Task<(string llmResponse, LlmCompletionResult llmResult)> GenerateLlmAnswerAndRecordMetricsAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var llmResult = await _llmService.GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            RequestSource.RagPipeline,
            cancellationToken).ConfigureAwait(false);

        var llmResponse = llmResult.Response;

        // Issue #1694: Record LLM token usage with OpenTelemetry GenAI semantic conventions
        if (llmResult.Usage != null && llmResult.Cost != null)
        {
            var tokenUsage = TokenUsage.FromLlmResult(llmResult.Usage, llmResult.Cost);

            Api.Observability.MeepleAiMetrics.RecordLlmTokenUsage(
                promptTokens: tokenUsage.PromptTokens,
                completionTokens: tokenUsage.CompletionTokens,
                totalTokens: tokenUsage.TotalTokens,
                modelId: tokenUsage.ModelId,
                provider: tokenUsage.Provider,
                operationDurationMs: null,
                costUsd: tokenUsage.EstimatedCost);

            _logger.LogInformation(
                "LLM token usage recorded: {TokenUsage}",
                tokenUsage);
        }

        return (llmResponse, llmResult);
    }

    /// <summary>
    /// Builds response with quality metrics, citations, and RAG validation.
    /// </summary>
    private async Task<QaResponseDto> BuildValidatedResponseAsync(
        AskQuestionQuery query,
        string llmResponse,
                List<SearchResultDto> searchResults,
        List<Domain.Entities.SearchResult> domainSearchResults,
        Confidence searchConfidence,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        // Calculate quality metrics
        var llmConfidence = _qualityTrackingService.CalculateLlmConfidence(llmResponse, domainSearchResults);
        var overallConfidence = _qualityTrackingService.CalculateOverallConfidence(searchConfidence, llmConfidence);
        var isLowQuality = _qualityTrackingService.IsLowQuality(overallConfidence);

        // Extract citations
        var citations = searchResults.Select(sr => new CitationDto(
            DocumentId: sr.VectorDocumentId,
            PageNumber: sr.PageNumber,
            Snippet: sr.TextContent.Substring(0, Math.Min(150, sr.TextContent.Length)),
            RelevanceScore: sr.RelevanceScore
        )).ToList();

        // ISSUE-977: BGAI-035 - Run RAG validation pipeline
        RagValidationResultDto? validationResult = null;
        try
        {
            var qaResponse = new Api.Models.QaResponse(
                answer: llmResponse,
                snippets: searchResults.Select(sr => new Api.Models.Snippet(
                    text: sr.TextContent,
                    source: $"PDF:{sr.VectorDocumentId}",
                    page: sr.PageNumber,
                    line: 0,
                    score: (float)sr.RelevanceScore
                )).ToList(),
                confidence: overallConfidence.Value
            );

            var validation = await _validationPipeline.ValidateWithMultiModelAsync(
                qaResponse,
                query.GameId.ToString(),
                systemPrompt,
                userPrompt,
                query.Language,
                cancellationToken).ConfigureAwait(false);

            validationResult = new RagValidationResultDto(
                IsValid: validation.IsValid,
                LayersPassed: validation.LayersPassed,
                TotalLayers: validation.TotalLayers,
                Message: validation.Message,
                Severity: validation.Severity.ToString(),
                DurationMs: validation.DurationMs
            );

            _logger.LogInformation(
                "RAG validation completed: IsValid={IsValid}, Layers={LayersPassed}/{TotalLayers}, Severity={Severity}",
                validation.IsValid, validation.LayersPassed, validation.TotalLayers, validation.Severity);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: QUERY HANDLER PATTERN - CQRS query boundary
        // RAG validation is non-critical optional enhancement. Generic catch prevents
        // validation failures from blocking the main response. Returns null validation result.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG validation pipeline failed, continuing without validation");
        }
#pragma warning restore CA1031

        return new QaResponseDto(
            Answer: llmResponse,
            Sources: searchResults,
            SearchConfidence: searchConfidence.Value,
            LlmConfidence: llmConfidence.Value,
            OverallConfidence: overallConfidence.Value,
            IsLowQuality: isLowQuality,
            Citations: citations,
            ValidationResult: validationResult
        );
    }

    private async Task<(bool allReady, int processing, int total)> CheckDocumentsReadyAsync(
        Guid gameId, List<Guid>? documentIds, CancellationToken ct)
    {
        var documents = documentIds?.Count > 0
            ? await _pdfDocumentRepository.GetByIdsAsync(documentIds, ct).ConfigureAwait(false)
            : await _pdfDocumentRepository.FindByGameIdAsync(gameId, ct).ConfigureAwait(false);

        // Defensive: Handle null or empty collection
        if (documents is null or { Count: 0 }) return (true, 0, 0);

        var notCompleted = documents.Count(d =>
            d.ProcessingState != PdfProcessingState.Ready);
        return (notCompleted == 0, notCompleted, documents.Count);
    }
}