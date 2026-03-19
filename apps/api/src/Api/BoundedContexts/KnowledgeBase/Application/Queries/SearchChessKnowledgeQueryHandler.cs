using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for SearchChessKnowledgeQuery.
/// Searches the chess knowledge base using vector similarity.
/// </summary>
internal sealed class SearchChessKnowledgeQueryHandler
    : IRequestHandler<SearchChessKnowledgeQuery, Api.Services.SearchResult>
{
    private readonly ILogger<SearchChessKnowledgeQueryHandler> _logger;

    public SearchChessKnowledgeQueryHandler(
        ILogger<SearchChessKnowledgeQueryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Api.Services.SearchResult> Handle(
        SearchChessKnowledgeQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("SearchChessKnowledge called for query: {Query} — returning empty (Qdrant removed)", request.Query);

        return Task.FromResult(Api.Services.SearchResult.CreateSuccess(new List<SearchResultItem>()));
    }
}
