using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handles VectorSemanticSearchQuery.
/// Embeds the query string via IEmbeddingService, then searches pgvector via IVectorStoreAdapter.
/// Supports single-game (GameId provided) and cross-game (all completed documents) search.
/// Task 4: Qdrant → pgvector migration.
/// </summary>
internal sealed class VectorSemanticSearchQueryHandler
    : IQueryHandler<VectorSemanticSearchQuery, VectorSemanticSearchResultDto>
{
    private const double DefaultMinScore = 0.0;

    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreAdapter _vectorStoreAdapter;
    private readonly ILogger<VectorSemanticSearchQueryHandler> _logger;

    public VectorSemanticSearchQueryHandler(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IVectorStoreAdapter vectorStoreAdapter,
        ILogger<VectorSemanticSearchQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStoreAdapter = vectorStoreAdapter ?? throw new ArgumentNullException(nameof(vectorStoreAdapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VectorSemanticSearchResultDto> Handle(
        VectorSemanticSearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Step 1: Embed the query string.
        _logger.LogDebug(
            "[VectorSemanticSearchQueryHandler] Generating embedding for query: {Query}",
            query.Query);

        var embeddingResult = await _embeddingService
            .GenerateEmbeddingAsync(query.Query, cancellationToken)
            .ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            var errorMessage = embeddingResult.ErrorMessage ?? "Embedding generation returned no vectors";
            _logger.LogWarning(
                "[VectorSemanticSearchQueryHandler] Embedding failed: {ErrorMessage}",
                errorMessage);

            return new VectorSemanticSearchResultDto(
                Results: new List<VectorSearchResultItem>(),
                ErrorMessage: errorMessage);
        }

        var queryVector = new Vector(embeddingResult.Embeddings[0]);

        // Step 2: Execute search — single game or all completed games.
        List<Domain.Entities.Embedding> embeddings;

        if (query.GameId.HasValue)
        {
            _logger.LogDebug(
                "[VectorSemanticSearchQueryHandler] Searching single game: {GameId}, limit={Limit}",
                query.GameId.Value, query.Limit);

            embeddings = await _vectorStoreAdapter
                .SearchAsync(
                    query.GameId.Value,
                    queryVector,
                    query.Limit,
                    DefaultMinScore,
                    documentIds: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // Collect all distinct GameIds that have at least one completed VectorDocument.
            var completedGameIds = await _dbContext.VectorDocuments
                .AsNoTracking()
                .Where(v => v.IndexingStatus == "completed" && v.GameId.HasValue)
                .Select(v => v.GameId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (completedGameIds.Count == 0)
            {
                _logger.LogInformation(
                    "[VectorSemanticSearchQueryHandler] No completed VectorDocuments found; returning empty result.");

                return new VectorSemanticSearchResultDto(
                    Results: new List<VectorSearchResultItem>(),
                    ErrorMessage: null);
            }

            _logger.LogDebug(
                "[VectorSemanticSearchQueryHandler] Searching {GameCount} games, limit={Limit}",
                completedGameIds.Count, query.Limit);

            embeddings = await _vectorStoreAdapter
                .SearchByMultipleGameIdsAsync(
                    completedGameIds,
                    queryVector,
                    query.Limit,
                    DefaultMinScore,
                    documentIds: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        // Step 3: Map domain Embedding entities to result items.
        var items = embeddings
            .Select(e => new VectorSearchResultItem(
                DocumentId: e.VectorDocumentId,
                Text: e.TextContent,
                ChunkIndex: e.ChunkIndex,
                PageNumber: e.PageNumber))
            .ToList();

        _logger.LogInformation(
            "[VectorSemanticSearchQueryHandler] Search complete: {ResultCount} results",
            items.Count);

        return new VectorSemanticSearchResultDto(Results: items, ErrorMessage: null);
    }
}
