using Grpc.Core;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;

namespace Api.Services.Qdrant;

/// <summary>
/// Handles vector indexing operations in Qdrant
/// </summary>
internal class QdrantVectorIndexer : IQdrantVectorIndexer
{
    private readonly IQdrantClientAdapter _clientAdapter;
    private readonly ILogger<QdrantVectorIndexer> _logger;
    private readonly TimeProvider _timeProvider;

    public QdrantVectorIndexer(
        IQdrantClientAdapter clientAdapter,
        ILogger<QdrantVectorIndexer> logger,
        TimeProvider? timeProvider = null)
    {
        _clientAdapter = clientAdapter;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Index a batch of vectors with their payloads
    /// </summary>
    public async Task UpsertPointsAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _clientAdapter.UpsertAsync(
                collectionName: collectionName,
                points: points,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            _logger.LogDebug("Successfully upserted {Count} points to collection {CollectionName}",
                points.Count, collectionName);
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // INFRASTRUCTURE LOGGING PATTERN: Log exceptions at the infrastructure boundary to provide
        // debugging context (collection name, operation type, error details) before propagating.
        // Callers may not have this context, so logging here is intentional.
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "Null argument while upserting points to collection {CollectionName}", collectionName);
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument while upserting points to collection {CollectionName}", collectionName);
            throw;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC error upserting points to collection {CollectionName}: {Status}",
                collectionName, ex.Status);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation while upserting points to collection {CollectionName}", collectionName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Operation cancelled while upserting points to collection {CollectionName}", collectionName);
            throw;
        }
#pragma warning restore S2139
    }

    /// <summary>
    /// Build point structures from document chunks
    /// </summary>
    public List<PointStruct> BuildPoints(
        List<DocumentChunk> chunks,
        Dictionary<string, Value> basePayload)
    {
        var points = new List<PointStruct>();

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var pointId = Guid.NewGuid().ToString();

            // Clone base payload and add chunk-specific data
            var payload = new Dictionary<string, Value>(basePayload, StringComparer.Ordinal)
            {
                ["chunk_index"] = i,
                ["text"] = chunk.Text,
                ["page"] = chunk.Page,
                ["char_start"] = chunk.CharStart,
                ["char_end"] = chunk.CharEnd,
                ["indexed_at"] = _timeProvider.GetUtcNow().UtcDateTime.ToString("o")
            };

            var point = new PointStruct
            {
                Id = new PointId { Uuid = pointId },
                Vectors = chunk.Embedding,
                Payload = { payload }
            };

            points.Add(point);
        }

        return points;
    }

    /// <summary>
    /// Delete vectors matching a filter
    /// </summary>
    public async Task DeleteByFilterAsync(
        string collectionName,
        Filter filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _clientAdapter.DeleteAsync(
                collectionName: collectionName,
                filter: filter,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            _logger.LogDebug("Successfully deleted vectors from collection {CollectionName}", collectionName);
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // INFRASTRUCTURE LOGGING PATTERN: Log exceptions at the infrastructure boundary for debugging.
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument while deleting vectors from collection {CollectionName}", collectionName);
            throw;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC error deleting vectors from collection {CollectionName}: {Status}",
                collectionName, ex.Status);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation while deleting vectors from collection {CollectionName}", collectionName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Operation cancelled while deleting vectors from collection {CollectionName}", collectionName);
            throw;
        }
#pragma warning restore S2139
    }
}
