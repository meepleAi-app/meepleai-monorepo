namespace Api.Services.Rag;

/// <summary>
/// Service for expanding queries with synonyms and alternative phrasings
/// to improve RAG recall (PERF-08)
/// </summary>
public interface IQueryExpansionService
{
    /// <summary>
    /// Generates query variations for improved recall
    /// </summary>
    /// <param name="query">Original query</param>
    /// <param name="language">Language code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of query variations including the original</returns>
    Task<List<string>> GenerateQueryVariationsAsync(
        string query,
        string language,
        CancellationToken cancellationToken = default);
}
