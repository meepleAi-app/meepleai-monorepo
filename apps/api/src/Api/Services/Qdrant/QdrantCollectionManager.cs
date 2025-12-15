using Grpc.Core;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;

namespace Api.Services.Qdrant;

/// <summary>
/// Manages Qdrant collection lifecycle operations
/// </summary>
internal class QdrantCollectionManager : IQdrantCollectionManager
{
    private readonly IQdrantClientAdapter _clientAdapter;
    private readonly ILogger<QdrantCollectionManager> _logger;

    public QdrantCollectionManager(
        IQdrantClientAdapter clientAdapter,
        ILogger<QdrantCollectionManager> logger)
    {
        _clientAdapter = clientAdapter;
        _logger = logger;
    }

    /// <summary>
    /// Check if collection exists
    /// </summary>
    public async Task<bool> CollectionExistsAsync(string collectionName, CancellationToken ct = default)
    {
        try
        {
            var collectionsResponse = await _clientAdapter.ListCollectionsAsync(ct).ConfigureAwait(false);
            return collectionsResponse.Any(c => string.Equals(c, collectionName, StringComparison.Ordinal));
        }
        catch (RpcException ex)
        {
            // S2139: Logging removed. Wrapped for context.
            throw new InvalidOperationException($"Failed to check if collection '{collectionName}' exists: {ex.Status}", ex);
        }
    }

    /// <summary>
    /// Ensure collection exists, creating it if necessary with proper indexes
    /// </summary>
    public async Task EnsureCollectionExistsAsync(string collectionName, uint vectorSize, CancellationToken ct = default)
    {
        try
        {
            var exists = await CollectionExistsAsync(collectionName, ct).ConfigureAwait(false);

            if (exists)
            {
                _logger.LogInformation("Collection {CollectionName} already exists", collectionName);
                return;
            }

            _logger.LogInformation("Creating collection {CollectionName} with vector size {VectorSize}",
                collectionName, vectorSize);

            await CreateCollectionAsync(collectionName, vectorSize, ct).ConfigureAwait(false);

            // Create payload indexes for filtering
            await CreatePayloadIndexAsync(collectionName, "game_id", PayloadSchemaType.Keyword, ct).ConfigureAwait(false);
            await CreatePayloadIndexAsync(collectionName, "pdf_id", PayloadSchemaType.Keyword, ct).ConfigureAwait(false);
            await CreatePayloadIndexAsync(collectionName, "category", PayloadSchemaType.Keyword, ct).ConfigureAwait(false);
            await CreatePayloadIndexAsync(collectionName, "language", PayloadSchemaType.Keyword, ct).ConfigureAwait(false);

            _logger.LogInformation("Collection {CollectionName} created successfully with indexes", collectionName);
        }
        catch (RpcException ex)
        {
            // S2139: Logging removed. Wrapped for context.
            throw new InvalidOperationException($"Failed to ensure collection '{collectionName}' exists: {ex.Status}", ex);
        }
    }

    /// <summary>
    /// Create a new collection with the specified vector configuration
    /// </summary>
    public async Task CreateCollectionAsync(string collectionName, uint vectorSize, CancellationToken ct = default)
    {
        await _clientAdapter.CreateCollectionAsync(
            collectionName: collectionName,
            vectorsConfig: new VectorParams
            {
                Size = vectorSize,
                Distance = Distance.Cosine
            },
            cancellationToken: ct
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Create payload index for filtering
    /// </summary>
    public async Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken ct = default)
    {
        await _clientAdapter.CreatePayloadIndexAsync(
            collectionName: collectionName,
            fieldName: fieldName,
            schemaType: schemaType,
            cancellationToken: ct
        ).ConfigureAwait(false);
    }
}
