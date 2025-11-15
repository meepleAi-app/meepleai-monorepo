using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Models;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for SearchChessKnowledgeQuery.
/// Searches the chess knowledge base using vector similarity.
/// </summary>
public sealed class SearchChessKnowledgeQueryHandler
    : IRequestHandler<SearchChessKnowledgeQuery, Api.Services.SearchResult>
{
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SearchChessKnowledgeQueryHandler> _logger;

    private const string ChessCategory = "chess";

    public SearchChessKnowledgeQueryHandler(
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        ILogger<SearchChessKnowledgeQueryHandler> logger)
    {
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Api.Services.SearchResult> Handle(
        SearchChessKnowledgeQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Searching chess knowledge: {Query}", request.Query);

            // Generate embedding for query
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
                request.Query,
                cancellationToken);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError(
                    "Failed to generate query embedding: {Error}",
                    embeddingResult.ErrorMessage);

                return Api.Services.SearchResult.CreateFailure("Failed to generate query embedding");
            }

            // Search by chess category
            var searchResult = await _qdrantService.SearchByCategoryAsync(
                ChessCategory,
                embeddingResult.Embeddings[0],
                request.Limit,
                cancellationToken);

            _logger.LogInformation(
                "Chess knowledge search completed: {ResultCount} results",
                searchResult.Results.Count);

            return searchResult;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // ERROR STATE MANAGEMENT: Chess knowledge search failures return structured error result
            // Rationale: Search involves multiple external systems (embedding API, Qdrant). Returning
            // a typed failure result allows callers to distinguish success/failure and display appropriate
            // error messages. Throwing would cause 500 errors without context about which search stage
            // failed (embedding generation vs vector search).
            // Context: Search failures typically from embedding API timeout or Qdrant unavailable
            _logger.LogError(ex, "Error during chess knowledge search");
            return Api.Services.SearchResult.CreateFailure($"Search error: {ex.Message}");
        }
    }
}
