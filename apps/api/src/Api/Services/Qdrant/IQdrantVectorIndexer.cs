using Qdrant.Client.Grpc;

namespace Api.Services.Qdrant;

/// <summary>
/// Handles vector indexing operations in Qdrant
/// </summary>
public interface IQdrantVectorIndexer
{
    /// <summary>
    /// Index a batch of vectors with their payloads
    /// </summary>
    Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        CancellationToken ct = default);

    /// <summary>
    /// Build point structures from document chunks
    /// </summary>
    List<PointStruct> BuildPoints(
        List<DocumentChunk> chunks,
        Dictionary<string, Value> basePayload);

    /// <summary>
    /// Delete vectors matching a filter
    /// </summary>
    Task DeleteByFilterAsync(
        string collectionName,
        Filter filter,
        CancellationToken ct = default);
}
