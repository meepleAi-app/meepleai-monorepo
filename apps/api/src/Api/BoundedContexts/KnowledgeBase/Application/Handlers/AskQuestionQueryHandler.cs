using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for AskQuestionQuery.
/// Orchestrates RAG (Retrieval-Augmented Generation) flow:
/// 1. Vector search for relevant context
/// 2. LLM generation with context
/// 3. Quality tracking and confidence scoring
/// </summary>
public class AskQuestionQueryHandler : IQueryHandler<AskQuestionQuery, QaResponseDto>
{
    private const string DefaultSystemPrompt =
        "You are MeepleAI, a helpful board game assistant. Answer using only the provided context. "
        + "Cite page numbers, stay concise, and say \"I don't know\" when the context is insufficient.";

    private readonly SearchQueryHandler _searchQueryHandler;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly ChatContextDomainService _chatContextService;
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly ILlmService _llmService;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly IRagValidationPipelineService _validationPipeline;
    private readonly ILogger<AskQuestionQueryHandler> _logger;

    public AskQuestionQueryHandler(
        SearchQueryHandler searchQueryHandler,
        QualityTrackingDomainService qualityTrackingService,
        ChatContextDomainService chatContextService,
        IChatThreadRepository chatThreadRepository,
        ILlmService llmService,
        IPromptTemplateService promptTemplateService,
        IRagValidationPipelineService validationPipeline,
        ILogger<AskQuestionQueryHandler> logger)
    {
        _searchQueryHandler = searchQueryHandler ?? throw new ArgumentNullException(nameof(searchQueryHandler));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _chatContextService = chatContextService ?? throw new ArgumentNullException(nameof(chatContextService));
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _promptTemplateService = promptTemplateService ?? throw new ArgumentNullException(nameof(promptTemplateService));
        _validationPipeline = validationPipeline ?? throw new ArgumentNullException(nameof(validationPipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QaResponseDto> Handle(
        AskQuestionQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing AskQuestionQuery: GameId={GameId}, Question={Question}",
            query.GameId, query.Question);

        // Step 1: Perform vector search (AI-14: configurable search mode)
        var searchQuery = new SearchQuery(
            GameId: query.GameId,
            Query: query.Question,
            TopK: 5,
            MinScore: 0.7,
            SearchMode: query.SearchMode ?? "hybrid", // Default to hybrid if not specified
            Language: query.Language
        );

        var searchResults = await _searchQueryHandler.Handle(searchQuery, cancellationToken);

        // Map search results to domain entities for quality tracking
        var domainSearchResults = searchResults.Select(sr => new Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.Parse(sr.VectorDocumentId),
            textContent: sr.TextContent,
            pageNumber: sr.PageNumber,
            relevanceScore: new Confidence(sr.RelevanceScore),
            rank: sr.Rank,
            searchMethod: sr.SearchMethod ?? "unknown"
        )).ToList();

        // Calculate search confidence
        var searchConfidence = _qualityTrackingService.CalculateSearchConfidence(domainSearchResults);

        // Step 1.5: Load chat thread context if ThreadId provided (Issue #857)
        string chatHistoryContext = string.Empty;
        if (query.ThreadId.HasValue)
        {
            var thread = await _chatThreadRepository.GetByIdAsync(query.ThreadId.Value, cancellationToken);
            if (thread != null)
            {
                // Security: Validate thread belongs to requested game (prevent cross-game data leak)
                if (thread.GameId.HasValue && thread.GameId.Value != query.GameId)
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

        // Step 2: Build LLM prompt with context
        var systemPrompt = await _promptTemplateService.GetActivePromptAsync("rag-system-prompt")
            ?? DefaultSystemPrompt;
        var context = string.Join("\n\n", searchResults.Select(sr =>
            $"[Page {sr.PageNumber}] {sr.TextContent}"));

        // Enrich user prompt with chat history if available
        var baseQuestion = $"Question: {query.Question}\n\nContext:\n{context}";
        var userPrompt = !string.IsNullOrWhiteSpace(chatHistoryContext)
            ? _chatContextService.EnrichPromptWithHistory(baseQuestion, chatHistoryContext)
            : baseQuestion;

        // Step 3: Generate answer with LLM
        var llmResult = await _llmService.GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            cancellationToken);

        var llmResponse = llmResult.Response;

        // Issue #1694: Record LLM token usage with OpenTelemetry GenAI semantic conventions
        if (llmResult.Usage != null && llmResult.Cost != null)
        {
            var tokenUsage = TokenUsage.FromLlmResult(llmResult.Usage, llmResult.Cost);

            // Record metrics with OpenTelemetry GenAI conventions
            Api.Observability.MeepleAiMetrics.RecordLlmTokenUsage(
                promptTokens: tokenUsage.PromptTokens,
                completionTokens: tokenUsage.CompletionTokens,
                totalTokens: tokenUsage.TotalTokens,
                modelId: tokenUsage.ModelId,
                provider: tokenUsage.Provider,
                operationDurationMs: null, // Duration tracked separately in activity
                costUsd: tokenUsage.EstimatedCost);

            _logger.LogInformation(
                "LLM token usage recorded: {TokenUsage}",
                tokenUsage);
        }

        // Calculate LLM confidence
        var llmConfidence = _qualityTrackingService.CalculateLlmConfidence(
            llmResponse,
            domainSearchResults);

        // Calculate overall confidence
        var overallConfidence = _qualityTrackingService.CalculateOverallConfidence(
            searchConfidence,
            llmConfidence);

        // Determine quality
        var isLowQuality = _qualityTrackingService.IsLowQuality(overallConfidence);

        // Extract citations (simplified - can be enhanced)
        var citations = searchResults.Select(sr => new CitationDto(
            DocumentId: sr.VectorDocumentId,
            PageNumber: sr.PageNumber,
            Snippet: sr.TextContent.Substring(0, Math.Min(150, sr.TextContent.Length)),
            RelevanceScore: sr.RelevanceScore
        )).ToList();

        // ISSUE-977: BGAI-035 - Run RAG validation pipeline (all 5 layers)
        RagValidationResultDto? validationResult = null;
        try
        {
            // Build QaResponse for validation
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

            // Run validation pipeline (multi-model mode: all 5 layers)
            var validation = await _validationPipeline.ValidateWithMultiModelAsync(
                qaResponse,
                query.GameId.ToString(),
                systemPrompt,
                userPrompt,
                query.Language,
                cancellationToken);

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
        catch (Exception ex)
        {
            // Log validation failure but don't block the response
            _logger.LogError(ex, "RAG validation pipeline failed, continuing without validation");
        }

        var response = new QaResponseDto(
            Answer: llmResponse,
            Sources: searchResults,
            SearchConfidence: searchConfidence.Value,
            LlmConfidence: llmConfidence.Value,
            OverallConfidence: overallConfidence.Value,
            IsLowQuality: isLowQuality,
            Citations: citations,
            ValidationResult: validationResult
        );

        _logger.LogInformation(
            "AskQuestionQuery completed: OverallConfidence={Confidence}, IsLowQuality={IsLowQuality}",
            overallConfidence.Value, isLowQuality);

        return response;
    }
}