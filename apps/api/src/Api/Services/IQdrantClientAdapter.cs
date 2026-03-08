namespace Api.Services;

/// <summary>
/// Stub adapter interface for vector store operations.
/// Previously wrapped Qdrant SDK — now pgvector is the sole vector store.
/// Retained for backward compatibility with admin endpoints and monitoring handlers
/// that query collection-level metrics.
/// </summary>
internal interface IQdrantClientAdapter
{
    Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets basic information about a collection (vector count, indexed count).
    /// Returns a simple DTO instead of the Qdrant SDK type.
    /// </summary>
    Task<VectorCollectionInfo> GetCollectionInfoAsync(
        string collectionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts points in a collection.
    /// </summary>
    Task<ulong> CountAsync(
        string collectionName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple DTO replacing Qdrant.Client.Grpc.CollectionInfo.
/// </summary>
internal sealed record VectorCollectionInfo(
    ulong PointsCount,
    ulong IndexedVectorsCount);
