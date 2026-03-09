using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Handler for vector store metrics query.
/// Uses Qdrant client to retrieve collection statistics.
/// Issue #3695: Resources Monitoring - Vector store metrics
/// </summary>
internal class GetVectorStoreMetricsQueryHandler : IQueryHandler<GetVectorStoreMetricsQuery, VectorStoreMetricsDto>
{
    private readonly IQdrantClientAdapter _qdrantClient;

    public GetVectorStoreMetricsQueryHandler(IQdrantClientAdapter qdrantClient)
    {
        _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
    }

    public async Task<VectorStoreMetricsDto> Handle(GetVectorStoreMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get list of all collections
        var collections = await _qdrantClient.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);

        // Get detailed info for each collection
        var collectionStats = new List<CollectionStatsDto>();
        long totalVectors = 0;
        long totalIndexed = 0;
        long totalMemory = 0;

        foreach (var collectionName in collections)
        {
            try
            {
                var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, cancellationToken).ConfigureAwait(false);

                var vectorCount = (long)info.PointsCount;
                var indexedCount = (long)info.IndexedVectorsCount;

                // pgvector replaced Qdrant — config details no longer available via this path
                var dimensions = 384; // Default for sentence-transformers
                var distanceMetric = "Cosine";

                // Estimate memory usage (rough calculation: vectors * dimensions * 4 bytes per float)
                var memoryBytes = vectorCount * dimensions * 4L;

                collectionStats.Add(new CollectionStatsDto(
                    CollectionName: collectionName,
                    VectorCount: vectorCount,
                    IndexedCount: indexedCount,
                    VectorDimensions: dimensions,
                    DistanceMetric: distanceMetric,
                    MemoryBytes: memoryBytes,
                    MemoryFormatted: FormatBytes(memoryBytes)
                ));

                totalVectors += vectorCount;
                totalIndexed += indexedCount;
                totalMemory += memoryBytes;
            }
            catch (Exception)
            {
                // Ignore errors for individual collections and continue processing
                // If a collection fails to load, skip it rather than failing the entire operation
            }
        }

        return new VectorStoreMetricsDto(
            TotalCollections: collections.Count,
            TotalVectors: totalVectors,
            IndexedVectors: totalIndexed,
            MemoryBytes: totalMemory,
            MemoryFormatted: FormatBytes(totalMemory),
            Collections: collectionStats,
            MeasuredAt: DateTime.UtcNow
        );
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
