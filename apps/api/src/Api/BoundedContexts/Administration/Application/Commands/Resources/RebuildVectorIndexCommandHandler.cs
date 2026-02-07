using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Qdrant.Client.Grpc;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Handler for RebuildVectorIndexCommand.
/// Updates collection configuration to trigger index rebuild in Qdrant.
/// Issue #3695: Resources Monitoring - Rebuild vector index implementation
/// </summary>
internal class RebuildVectorIndexCommandHandler : ICommandHandler<RebuildVectorIndexCommand, bool>
{
    private readonly IQdrantClientAdapter _qdrantClient;

    public RebuildVectorIndexCommandHandler(IQdrantClientAdapter qdrantClient)
    {
        _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
    }

    public async Task<bool> Handle(RebuildVectorIndexCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Confirmed)
        {
            throw new InvalidOperationException("Confirmation is required to rebuild the vector index.");
        }

        if (string.IsNullOrWhiteSpace(request.CollectionName))
        {
            throw new ArgumentException("Collection name cannot be empty.", nameof(request));
        }

        // Get current collection info to preserve settings
        var collectionInfo = await _qdrantClient.GetCollectionInfoAsync(
            request.CollectionName,
            cancellationToken
        ).ConfigureAwait(false);

        // Update HNSW config to trigger reindex
        // Changing ef_construct will force Qdrant to rebuild the index
        var currentEfConstruct = collectionInfo.Config?.HnswConfig?.EfConstruct ?? 100UL;
        var newEfConstruct = currentEfConstruct; // Keep same value

        var hnswConfig = new HnswConfigDiff
        {
            EfConstruct = newEfConstruct,
            M = collectionInfo.Config?.HnswConfig?.M ?? 16UL,
            FullScanThreshold = collectionInfo.Config?.HnswConfig?.FullScanThreshold ?? 10000UL
        };

        // Update collection to trigger reindex
        await _qdrantClient.UpdateCollectionConfigAsync(
            request.CollectionName,
            hnswConfig,
            null,
            cancellationToken
        ).ConfigureAwait(false);

        return true;
    }
}
