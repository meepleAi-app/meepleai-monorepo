using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for SearchQuery.
/// Orchestrates vector/hybrid search using domain services.
/// </summary>
public class SearchQueryHandler : IQueryHandler<SearchQuery, List<SearchResultDto>>
{
    private readonly IEmbeddingRepository _embeddingRepository;
    private readonly VectorSearchDomainService _vectorSearchService;
    private readonly RrfFusionDomainService _rrfFusionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly ILogger<SearchQueryHandler> _logger;

    public SearchQueryHandler(
        IEmbeddingRepository embeddingRepository,
        VectorSearchDomainService vectorSearchService,
        RrfFusionDomainService rrfFusionService,
        IEmbeddingService embeddingService,
        IHybridSearchService hybridSearchService,
        ILogger<SearchQueryHandler> logger)
    {
        _embeddingRepository = embeddingRepository ?? throw new ArgumentNullException(nameof(embeddingRepository));
        _vectorSearchService = vectorSearchService ?? throw new ArgumentNullException(nameof(vectorSearchService));
        _rrfFusionService = rrfFusionService ?? throw new ArgumentNullException(nameof(rrfFusionService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<SearchResultDto>> Handle(
        SearchQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing SearchQuery: GameId={GameId}, Query={Query}, Mode={SearchMode}, TopK={TopK}",
            query.GameId, query.Query, query.SearchMode, query.TopK);

        // Validate search parameters
        _vectorSearchService.ValidateSearchParameters(query.TopK, query.MinScore);

        // Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            query.Query,
            query.Language,
            cancellationToken);

        var queryVector = new Vector(queryEmbedding.ToFloatArray());

        // Execute search based on mode
        var searchResults = query.SearchMode.ToLowerInvariant() switch
        {
            "vector" => await PerformVectorSearchAsync(
                query.GameId, queryVector, query.TopK, query.MinScore, cancellationToken),

            "hybrid" => await PerformHybridSearchAsync(
                query.GameId, queryVector, query.Query, query.TopK, query.MinScore, cancellationToken),

            _ => throw new ArgumentException($"Invalid search mode: {query.SearchMode}", nameof(query))
        } ?? new List<Domain.Entities.SearchResult>();

        // Map to DTOs
        var dtos = searchResults.Select(sr => new SearchResultDto(
            VectorDocumentId: sr.VectorDocumentId.ToString(),
            TextContent: sr.TextContent,
            PageNumber: sr.PageNumber,
            RelevanceScore: sr.RelevanceScore.Value,
            Rank: sr.Rank,
            SearchMethod: sr.SearchMethod
        )).ToList();

        _logger.LogInformation(
            "SearchQuery completed: Found {ResultCount} results",
            dtos.Count);

        return dtos;
    }

    private async Task<List<Domain.Entities.SearchResult>> PerformVectorSearchAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        CancellationToken cancellationToken)
    {
        // Get candidate embeddings from repository
        var embeddings = await _embeddingRepository.SearchByVectorAsync(
            gameId, queryVector, topK, minScore, cancellationToken);

        // Use domain service to rank results
        return _vectorSearchService.Search(queryVector, embeddings, topK, minScore);
    }

    private async Task<List<Domain.Entities.SearchResult>> PerformHybridSearchAsync(
        Guid gameId,
        Vector queryVector,
        string query,
        int topK,
        double minScore,
        CancellationToken cancellationToken)
    {
        // Vector search
        var vectorResults = await PerformVectorSearchAsync(
            gameId, queryVector, topK, minScore, cancellationToken);

        // Keyword search (use HybridSearchService with Keyword mode)
        var hybridSearchResults = await _hybridSearchService.SearchAsync(
            query,
            gameId,
            SearchMode.Keyword,
            topK,
            cancellationToken: cancellationToken);

        // Map keyword results to domain entities
        var keywordResults = hybridSearchResults
            .Select((kr, index) => kr.ToDomainSearchResult(index + 1))
            .ToList();

        // Use RRF fusion domain service
        return _rrfFusionService.FuseResults(vectorResults, keywordResults);
    }
}
