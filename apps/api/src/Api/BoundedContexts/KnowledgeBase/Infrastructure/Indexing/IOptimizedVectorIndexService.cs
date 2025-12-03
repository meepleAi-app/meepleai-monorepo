using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Indexing;

/// <summary>
/// Interface for the optimized vector index service.
/// ADR-016 Phase 3: Provides domain-driven interface for Qdrant collection management.
/// </summary>
public interface IOptimizedVectorIndexService
{
    /// <summary>
    /// Ensures an optimized collection exists with HNSW and quantization configuration.
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="vectorDimension">Vector dimension (default: 3072 for text-embedding-3-large)</param>
    /// <param name="hnswConfig">HNSW configuration (default: ADR-016 Phase 3 settings)</param>
    /// <param name="quantizationConfig">Quantization configuration (default: ADR-016 Phase 3 settings)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnsureOptimizedCollectionAsync(
        string collectionName,
        uint vectorDimension = OptimizedVectorIndexService.DefaultVectorDimension,
        HnswConfiguration? hnswConfig = null,
        QuantizationConfiguration? quantizationConfig = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new optimized collection with HNSW and quantization configuration.
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="vectorDimension">Vector dimension</param>
    /// <param name="hnswConfig">HNSW configuration</param>
    /// <param name="quantizationConfig">Quantization configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateOptimizedCollectionAsync(
        string collectionName,
        uint vectorDimension,
        HnswConfiguration hnswConfig,
        QuantizationConfiguration quantizationConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing collection's HNSW and quantization configuration.
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="hnswConfig">HNSW configuration</param>
    /// <param name="quantizationConfig">Quantization configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateCollectionConfigurationAsync(
        string collectionName,
        HnswConfiguration hnswConfig,
        QuantizationConfiguration quantizationConfig,
        CancellationToken cancellationToken = default);
}
