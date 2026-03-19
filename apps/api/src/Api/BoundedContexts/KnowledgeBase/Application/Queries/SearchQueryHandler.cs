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

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for SearchQuery.
/// Orchestrates vector/hybrid search using domain services.
/// </summary>
internal class SearchQueryHandler : IQueryHandler<SearchQuery, List<SearchResultDto>>
{
    private readonly IEmbeddingRepository _embeddingRepository;
    private readonly VectorSearchDomainService _vectorSearchService;
    private readonly RrfFusionDomainService _rrfFusionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly IRagAccessService _ragAccessService;
    private readonly ILogger<SearchQueryHandler> _logger;

    public SearchQueryHandler(
        IEmbeddingRepository embeddingRepository,
        VectorSearchDomainService vectorSearchService,
        RrfFusionDomainService rrfFusionService,
        IEmbeddingService embeddingService,
        IHybridSearchService hybridSearchService,
        IRagAccessService ragAccessService,
        ILogger<SearchQueryHandler> logger)
    {
        _embeddingRepository = embeddingRepository ?? throw new ArgumentNullException(nameof(embeddingRepository));
        _vectorSearchService = vectorSearchService ?? throw new ArgumentNullException(nameof(vectorSearchService));
        _rrfFusionService = rrfFusionService ?? throw new ArgumentNullException(nameof(rrfFusionService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<SearchResultDto>> Handle(
        SearchQuery query,
        CancellationToken cancellationToken)
    {
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
            "[SearchQueryHandler] ENTRY - Processing SearchQuery: GameId={GameId}, Query={Query}, Mode={SearchMode}, TopK={TopK}",
            query.GameId, query.Query, query.SearchMode, query.TopK);

        // Validate search parameters
        _vectorSearchService.ValidateSearchParameters(query.TopK, query.MinScore);

        // Generate query embedding
        _logger.LogDebug("[SearchQueryHandler] Step 1: Generating embedding via IEmbeddingService...");
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            query.Query,
            query.Language,
            cancellationToken).ConfigureAwait(false);
        sw1.Stop();
        _logger.LogInformation("[SearchQueryHandler] Step 1 DONE: Embedding generated in {ElapsedMs}ms - Success: {Success}",
            sw1.ElapsedMilliseconds, queryEmbedding.Success);

        var queryVector = new Vector(queryEmbedding.ToFloatArray());

        // Execute search based on mode
        _logger.LogDebug("[SearchQueryHandler] Step 2: Executing {SearchMode} search...", query.SearchMode);
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var searchResults = query.SearchMode.ToLowerInvariant() switch
        {
            "vector" => await PerformVectorSearchAsync(
                query.GameId, queryVector, query.TopK, query.MinScore, query.DocumentIds, cancellationToken).ConfigureAwait(false),

            "hybrid" => await PerformHybridSearchAsync(
                query.GameId, queryVector, query.Query, query.TopK, query.MinScore, query.DocumentIds, cancellationToken).ConfigureAwait(false),

            _ => throw new ArgumentException($"Invalid search mode: {query.SearchMode}", nameof(query))
        } ?? new List<Domain.Entities.SearchResult>();
        sw2.Stop();
        _logger.LogInformation("[SearchQueryHandler] Step 2 DONE: {SearchMode} search completed in {ElapsedMs}ms - {ResultCount} results",
            query.SearchMode, sw2.ElapsedMilliseconds, searchResults.Count);

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
            "[SearchQueryHandler] COMPLETE - SearchQuery completed: Found {ResultCount} results",
            dtos.Count);

        return dtos;
    }

    private async Task<List<Domain.Entities.SearchResult>> PerformVectorSearchAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        // Get candidate embeddings from repository (already filtered and ranked by Qdrant)
        var embeddings = await _embeddingRepository.SearchByVectorAsync(
            gameId, queryVector, topK, minScore, documentIds, cancellationToken).ConfigureAwait(false);

        if (embeddings == null)
        {
            _logger.LogWarning("Vector search returned null for gameId={GameId}", gameId);
            embeddings = new List<Domain.Entities.Embedding>();
        }

        _logger.LogInformation(
            "Vector search returned {Count} embeddings for gameId={GameId}",
            embeddings.Count, gameId);

        // Convert embeddings to SearchResults directly
        // Note: Qdrant already scored and filtered results by minScore, so we use rank-based scoring
        // This avoids recalculating cosine similarity with placeholder vectors (which would always be 0)
        var results = embeddings.Select((embedding, index) =>
        {
            // Calculate a score based on rank (first result gets highest score, decays with rank)
            // This preserves Qdrant's ranking while providing a meaningful confidence value
            var rankBasedScore = 1.0 - (index * 0.05); // First = 1.0, Second = 0.95, etc.
            var clampedScore = Math.Max(minScore, Math.Min(1.0, rankBasedScore));
            var confidence = new Confidence(clampedScore);

            return new Domain.Entities.SearchResult(
                id: Guid.NewGuid(),
                vectorDocumentId: embedding.VectorDocumentId,
                textContent: embedding.TextContent,
                pageNumber: embedding.PageNumber,
                relevanceScore: confidence,
                rank: index + 1,
                searchMethod: "vector"
            );
        }).ToList();

        _logger.LogInformation(
            "Converted {Count} embeddings to SearchResults with rank-based scoring",
            results.Count);

        return results;
    }

    private async Task<List<Domain.Entities.SearchResult>> PerformHybridSearchAsync(
        Guid gameId,
        Vector queryVector,
        string query,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        // Vector search
        var vectorResults = await PerformVectorSearchAsync(
            gameId, queryVector, topK, minScore, documentIds, cancellationToken).ConfigureAwait(false);

        // Keyword search (use HybridSearchService with Keyword mode)
        var hybridSearchResults = await _hybridSearchService.SearchAsync(
            query,
            gameId,
            SearchMode.Keyword,
            topK,
            documentIds?.ToList(), // Issue #2051: Pass document filter
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Map keyword results to domain entities
        var keywordResults = hybridSearchResults
            .Select((kr, index) => kr.ToDomainSearchResult(index + 1))
            .ToList();

        // Use RRF fusion domain service
        return _rrfFusionService.FuseResults(vectorResults, keywordResults);
    }
}
