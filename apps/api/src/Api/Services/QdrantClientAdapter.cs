using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Globalization;

namespace Api.Services;

internal class QdrantClientAdapter : IQdrantClientAdapter
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantClientAdapter> _logger;

    public QdrantClientAdapter(
        IConfiguration configuration,
        ILogger<QdrantClientAdapter> logger,
        Func<string, int, bool, QdrantClient>? clientFactory = null)
    {
        // S1075: Default Qdrant URL extracted to const
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        const string DefaultQdrantUrl = "http://localhost:6333";
#pragma warning restore S1075

        _logger = logger;
        var qdrantUrl = configuration["QDRANT_URL"] ?? DefaultQdrantUrl;

        if (!Uri.TryCreate(qdrantUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid QDRANT_URL value: {qdrantUrl}");
        }

        var host = uri.Host;
        var useHttps = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var grpcPort = ResolveGrpcPort(configuration, uri, logger);

        var factory = clientFactory ?? ((h, p, https) => new QdrantClient(h, p, https));
        _client = factory(host, grpcPort, useHttps);
        _logger.LogInformation("Qdrant client initialized for {Host}:{Port} (HTTPS: {UseHttps})", host, grpcPort, useHttps);
    }

    private static int ResolveGrpcPort(
        IConfiguration configuration,
        Uri uri,
        ILogger logger)
    {
        var grpcPortSetting = configuration["QDRANT_GRPC_PORT"];
        if (!string.IsNullOrWhiteSpace(grpcPortSetting))
        {
            if (int.TryParse(grpcPortSetting, NumberStyles.Integer, CultureInfo.InvariantCulture, out var explicitPort) && explicitPort > 0)
            {
                return explicitPort;
            }

            logger.LogWarning("Invalid QDRANT_GRPC_PORT value '{Value}', falling back to URI/default port.", grpcPortSetting);
        }

        if (!uri.IsDefaultPort)
        {
            if (uri.Port == 6333)
            {
                // Default REST port maps to 6334 for gRPC unless explicitly overridden
                return 6334;
            }

            return uri.Port;
        }

        return 6334;
    }

    public Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        return _client.ListCollectionsAsync(cancellationToken: cancellationToken);
    }

    public Task CreateCollectionAsync(
        string collectionName,
        VectorParams vectorsConfig,
        CancellationToken cancellationToken = default)
    {
        return _client.CreateCollectionAsync(collectionName, vectorsConfig, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// ADR-016 Phase 3: Creates a collection with optimized HNSW and quantization configuration.
    /// </summary>
    public async Task CreateCollectionWithConfigAsync(
        string collectionName,
        VectorParams vectorsConfig,
        HnswConfigDiff? hnswConfig = null,
        QuantizationConfig? quantizationConfig = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating collection {CollectionName} with HNSW config: m={M}, ef_construct={EfConstruct}",
            collectionName,
            hnswConfig?.M,
            hnswConfig?.EfConstruct);

        // Apply HNSW and quantization config to vector params if provided
        if (hnswConfig != null)
        {
            vectorsConfig.HnswConfig = hnswConfig;
        }

        if (quantizationConfig != null)
        {
            vectorsConfig.QuantizationConfig = quantizationConfig;
        }

        await _client.CreateCollectionAsync(
            collectionName,
            vectorsConfig,
            hnswConfig: hnswConfig,
            quantizationConfig: quantizationConfig,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Collection {CollectionName} created successfully with optimized indexing", collectionName);
    }

    /// <summary>
    /// ADR-016 Phase 3: Updates an existing collection's HNSW and quantization configuration.
    /// </summary>
    public async Task UpdateCollectionConfigAsync(
        string collectionName,
        HnswConfigDiff? hnswConfig = null,
        QuantizationConfigDiff? quantizationConfig = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating collection {CollectionName} configuration",
            collectionName);

        await _client.UpdateCollectionAsync(
            collectionName,
            hnswConfig: hnswConfig,
            quantizationConfig: quantizationConfig,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Collection {CollectionName} configuration updated successfully", collectionName);
    }

    public Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken cancellationToken = default)
    {
        return _client.CreatePayloadIndexAsync(collectionName, fieldName, schemaType, cancellationToken: cancellationToken);
    }

    public Task UpsertAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        CancellationToken cancellationToken = default)
    {
        return _client.UpsertAsync(collectionName, points.ToList(), cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        string collectionName,
        float[] vector,
        Filter? filter = default,
        ulong? limit = null,
        CancellationToken cancellationToken = default)
    {
        return _client.SearchAsync(
            collectionName: collectionName,
            vector: vector,
            filter: filter,
            limit: limit ?? 10,
            cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(
        string collectionName,
        Filter filter,
        CancellationToken cancellationToken = default)
    {
        return _client.DeleteAsync(collectionName, filter, cancellationToken: cancellationToken);
    }

    public Task<CollectionInfo> GetCollectionInfoAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        return _client.GetCollectionInfoAsync(collectionName, cancellationToken: cancellationToken);
    }

    public async Task DeleteCollectionAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Deleting Qdrant collection {CollectionName}", collectionName);
        await _client.DeleteCollectionAsync(collectionName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Collection {CollectionName} deleted successfully", collectionName);
    }

    public async Task<IReadOnlyList<RetrievedPoint>> ScrollPointsAsync(
        string collectionName,
        Filter? filter = null,
        uint limit = 20,
        PointId? offset = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.ScrollAsync(
            collectionName,
            filter: filter,
            limit: limit,
            offset: offset,
            payloadSelector: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response.Result.ToList();
    }

    public async Task<ulong> CountAsync(
        string collectionName,
        Filter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.CountAsync(
            collectionName,
            filter: filter,
            exact: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return result;
    }
}
