namespace Api.Services;

/// <summary>
/// No-op implementation of IQdrantClientAdapter.
/// Qdrant has been replaced by pgvector (PgVectorStoreAdapter).
/// This stub prevents DI resolution failures for admin/monitoring handlers
/// that still depend on IQdrantClientAdapter.
/// </summary>
internal sealed class NoOpQdrantClientAdapter : IQdrantClientAdapter
{
    public Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<VectorCollectionInfo> GetCollectionInfoAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new VectorCollectionInfo(PointsCount: 0, IndexedVectorsCount: 0));

    public Task<ulong> CountAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0UL);
}
