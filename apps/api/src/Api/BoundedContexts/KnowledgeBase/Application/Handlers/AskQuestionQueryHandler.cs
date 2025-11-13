using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
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
    private readonly ILlmService _llmService;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly ILogger<AskQuestionQueryHandler> _logger;

    public AskQuestionQueryHandler(
        SearchQueryHandler searchQueryHandler,
        QualityTrackingDomainService qualityTrackingService,
        ILlmService llmService,
        IPromptTemplateService promptTemplateService,
        ILogger<AskQuestionQueryHandler> logger)
    {
        _searchQueryHandler = searchQueryHandler ?? throw new ArgumentNullException(nameof(searchQueryHandler));
        _qualityTrackingService = qualityTrackingService ?? throw new ArgumentNullException(nameof(qualityTrackingService));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _promptTemplateService = promptTemplateService ?? throw new ArgumentNullException(nameof(promptTemplateService));
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

        // Step 2: Build LLM prompt with context
        var systemPrompt = await _promptTemplateService.GetActivePromptAsync("rag-system-prompt")
            ?? DefaultSystemPrompt;
        var context = string.Join("\n\n", searchResults.Select(sr =>
            $"[Page {sr.PageNumber}] {sr.TextContent}"));

        var userPrompt = $"Question: {query.Question}\n\nContext:\n{context}";

        // Step 3: Generate answer with LLM
        var llmResult = await _llmService.GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            cancellationToken);

        var llmResponse = llmResult.Response;

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

        var response = new QaResponseDto(
            Answer: llmResponse,
            Sources: searchResults,
            SearchConfidence: searchConfidence.Value,
            LlmConfidence: llmConfidence.Value,
            OverallConfidence: overallConfidence.Value,
            IsLowQuality: isLowQuality,
            Citations: citations
        );

        _logger.LogInformation(
            "AskQuestionQuery completed: OverallConfidence={Confidence}, IsLowQuality={IsLowQuality}",
            overallConfidence.Value, isLowQuality);

        return response;
    }
}
