using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Handler for vector store metrics query.
/// Vector store (Qdrant) has been removed — returns empty metrics.
/// Issue #3695: Resources Monitoring - Vector store metrics
/// </summary>
internal class GetVectorStoreMetricsQueryHandler : IQueryHandler<GetVectorStoreMetricsQuery, VectorStoreMetricsDto>
{
    public Task<VectorStoreMetricsDto> Handle(GetVectorStoreMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Vector store (Qdrant) has been removed — return empty metrics.
        return Task.FromResult(new VectorStoreMetricsDto(
            TotalCollections: 0,
            TotalVectors: 0,
            IndexedVectors: 0,
            MemoryBytes: 0,
            MemoryFormatted: "0 B",
            Collections: new List<CollectionStatsDto>(),
            MeasuredAt: DateTime.UtcNow
        ));
    }
}
