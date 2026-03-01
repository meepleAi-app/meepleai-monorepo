using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Qdrant.Client.Grpc;

namespace Api.Services;

internal interface IQdrantClientAdapter
{
    Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default);
    Task CreateCollectionAsync(
        string collectionName,
        VectorParams vectorsConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-016 Phase 3: Creates a collection with optimized HNSW and quantization configuration.
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="vectorsConfig">Vector parameters (size, distance)</param>
    /// <param name="hnswConfig">HNSW index configuration (m, ef_construct, full_scan_threshold)</param>
    /// <param name="quantizationConfig">Scalar quantization configuration (int8, quantile, always_ram)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateCollectionWithConfigAsync(
        string collectionName,
        VectorParams vectorsConfig,
        HnswConfigDiff? hnswConfig = null,
        QuantizationConfig? quantizationConfig = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ADR-016 Phase 3: Updates an existing collection's HNSW and quantization configuration.
    /// </summary>
    Task UpdateCollectionConfigAsync(
        string collectionName,
        HnswConfigDiff? hnswConfig = null,
        QuantizationConfigDiff? quantizationConfig = null,
        CancellationToken cancellationToken = default);

    Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken cancellationToken = default);
    Task UpsertAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        string collectionName,
        float[] vector,
        Filter? filter = default,
        ulong? limit = null,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(
        string collectionName,
        Filter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific collection.
    /// Issue #3695: Resources Monitoring - Vector store metrics
    /// </summary>
    Task<CollectionInfo> GetCollectionInfoAsync(
        string collectionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entire collection and all its data.
    /// Issue #4877: Qdrant Admin Operations
    /// </summary>
    Task DeleteCollectionAsync(
        string collectionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scrolls through points in a collection with optional filter and pagination.
    /// Returns raw ScrollResponse for admin browsing.
    /// Issue #4877: Qdrant Admin Operations
    /// </summary>
    Task<IReadOnlyList<RetrievedPoint>> ScrollPointsAsync(
        string collectionName,
        Filter? filter = null,
        uint limit = 20,
        PointId? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts points in a collection with optional filter.
    /// Issue #4877: Qdrant Admin Operations
    /// </summary>
    Task<ulong> CountAsync(
        string collectionName,
        Filter? filter = null,
        CancellationToken cancellationToken = default);
}
