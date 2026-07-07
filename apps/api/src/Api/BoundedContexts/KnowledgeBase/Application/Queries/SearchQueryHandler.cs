using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
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

        // Step 1: Resolve query embedding.
        // Issue #563: When the caller supplies a pre-computed QueryVector (e.g., AskQuestionQueryHandler
        // already produced one for semantic cache lookup), reuse it to avoid a duplicate embedding call
        // (~50–200ms savings per cache miss). Otherwise fall back to generating one here.
        Vector queryVector;
        if (query.QueryVector is { Count: > 0 } precomputed)
        {
            _logger.LogDebug(
                "[SearchQueryHandler] Step 1: Reusing caller-supplied query vector (Dim={Dim}) — skipping embedding call",
                precomputed.Count);
            // Materialize to float[] (Vector ctor requires array). ToArray() is a no-op when caller passes float[].
            queryVector = new Vector(precomputed as float[] ?? precomputed.ToArray());
        }
        else
        {
            _logger.LogDebug("[SearchQueryHandler] Step 1: Generating embedding via IEmbeddingService...");
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                query.Query,
                query.Language,
                cancellationToken).ConfigureAwait(false);
            sw1.Stop();
            _logger.LogInformation("[SearchQueryHandler] Step 1 DONE: Embedding generated in {ElapsedMs}ms - Success: {Success}",
                sw1.ElapsedMilliseconds, queryEmbedding.Success);

            queryVector = new Vector(queryEmbedding.ToFloatArray());
        }

        // Execute search based on mode
        _logger.LogDebug("[SearchQueryHandler] Step 2: Executing {SearchMode} search...", query.SearchMode);
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var searchResults = query.SearchMode.ToLowerInvariant() switch
        {
            "vector" => await PerformVectorSearchAsync(
                query.GameId, queryVector, query.TopK, query.MinScore, query.DocumentIds, cancellationToken).ConfigureAwait(false),

            // Phase D (D6): forward QueryRoleHint to the hybrid search re-ranker.
            "hybrid" => await PerformHybridSearchAsync(
                query.GameId, queryVector, query.Query, query.TopK, query.MinScore, query.DocumentIds, query.QueryRoleHint, cancellationToken).ConfigureAwait(false),

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
        // Issue #2712: use SearchByVectorWithScoresAsync so each result carries the REAL cosine
        // similarity computed by pgvector in SQL (1 - (vector <=> query)). The previous call
        // (SearchByVectorAsync) returned placeholder vectors and forced a rank-based score
        // (1.0 - index*0.05), which made the downstream confidence degenerate (always ≈0.53) and
        // hid grounded answers behind the "Non sono certo" card.
        var scoredEmbeddings = await _embeddingRepository.SearchByVectorWithScoresAsync(
            gameId, queryVector, topK, minScore, documentIds, cancellationToken).ConfigureAwait(false)
            ?? (IReadOnlyList<Domain.Entities.ScoredEmbedding>)Array.Empty<Domain.Entities.ScoredEmbedding>();

        _logger.LogInformation(
            "Vector search returned {Count} scored embeddings for gameId={GameId}",
            scoredEmbeddings.Count, gameId);

        // Results come pre-ordered by pgvector (ORDER BY distance), so the list index is the rank.
        var results = scoredEmbeddings.Select((scored, index) =>
        {
            var cosine = Math.Clamp(scored.Score, 0.0, 1.0);

            return new Domain.Entities.SearchResult(
                id: Guid.NewGuid(),
                vectorDocumentId: scored.Embedding.VectorDocumentId,
                textContent: scored.Embedding.TextContent,
                pageNumber: scored.Embedding.PageNumber,
                relevanceScore: new Confidence(cosine),
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
        GameBookRole queryRoleHint,
        CancellationToken cancellationToken)
    {
        // Vector search
        var vectorResults = await PerformVectorSearchAsync(
            gameId, queryVector, topK, minScore, documentIds, cancellationToken).ConfigureAwait(false);

        // Keyword search (use HybridSearchService with Keyword mode)
        // Issue #423: Pass keywordMinScore to filter low-relevance keyword matches (e.g., ToC entries)
        // ts_rank_cd scores are typically 0-0.3; threshold of 0.01 filters noise while keeping real matches
        // Phase D (D6): queryRoleHint enables role-match boost in the re-ranker.
        const double KeywordMinScore = 0.01;
        var hybridSearchResults = await _hybridSearchService.SearchAsync(
            query,
            gameId,
            SearchMode.Keyword,
            topK,
            documentIds?.ToList(), // Issue #2051: Pass document filter
            keywordMinScore: KeywordMinScore,
            queryRoleHint: queryRoleHint,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Map keyword results to domain entities
        var keywordResults = hybridSearchResults
            .Select((kr, index) => kr.ToDomainSearchResult(index + 1))
            .ToList();

        // Use RRF fusion domain service
        return _rrfFusionService.FuseResults(vectorResults, keywordResults);
    }
}
