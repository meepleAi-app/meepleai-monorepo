using Qdrant.Client.Grpc;

namespace Api.Services.Qdrant;

/// <summary>
/// Manages Qdrant collection lifecycle operations (create, check existence, delete)
/// </summary>
internal interface IQdrantCollectionManager
{
    /// <summary>
    /// Check if the collection exists
    /// </summary>
    Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure collection exists, creating it if necessary with proper indexes
    /// </summary>
    Task EnsureCollectionExistsAsync(string collectionName, uint vectorSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new collection with the specified vector configuration
    /// </summary>
    Task CreateCollectionAsync(string collectionName, uint vectorSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create payload index for filtering
    /// </summary>
    Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken cancellationToken = default);
}
