namespace Api.Services.Rag;

/// <summary>
/// Service for reranking search results using Reciprocal Rank Fusion (RRF)
/// </summary>
internal interface ISearchResultReranker
{
    /// <summary>
    /// Fuses multiple search result lists using RRF algorithm
    /// </summary>
    /// <param name="searchResults">List of search results from multiple queries</param>
    /// <returns>Fused and reranked results</returns>
    Task<List<SearchResultItem>> FuseSearchResultsAsync(List<SearchResult> searchResults);
}
