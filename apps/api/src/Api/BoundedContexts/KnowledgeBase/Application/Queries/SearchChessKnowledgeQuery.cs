using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to search chess knowledge base.
/// Returns relevant chess knowledge chunks based on the query.
/// </summary>
internal sealed record SearchChessKnowledgeQuery : IRequest<Api.Services.SearchResult>
{
    /// <summary>
    /// Search query text.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Maximum number of results to return (default: 5).
    /// </summary>
    public int Limit { get; init; } = 5;
}
