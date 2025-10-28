using Qdrant.Client.Grpc;

namespace Api.Services.Qdrant;

/// <summary>
/// Handles vector search operations in Qdrant
/// </summary>
public interface IQdrantVectorSearcher
{
    /// <summary>
    /// Search for similar vectors using the query embedding and filter
    /// </summary>
    Task<List<ScoredPoint>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        Filter? filter = null,
        int limit = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Convert Qdrant search results to domain model
    /// </summary>
    List<SearchResultItem> ConvertToSearchResults(IEnumerable<ScoredPoint> scoredPoints);

    /// <summary>
    /// Build a filter for game ID
    /// </summary>
    Filter BuildGameFilter(string gameId);

    /// <summary>
    /// Build a filter for game ID and language
    /// </summary>
    Filter BuildGameLanguageFilter(string gameId, string language);

    /// <summary>
    /// Build a filter for category
    /// </summary>
    Filter BuildCategoryFilter(string category);

    /// <summary>
    /// Build a filter for PDF ID
    /// </summary>
    Filter BuildPdfFilter(string pdfId);
}
