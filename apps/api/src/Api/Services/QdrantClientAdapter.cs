using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Api.Services;

public class QdrantClientAdapter : IQdrantClientAdapter
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantClientAdapter> _logger;

    public QdrantClientAdapter(
        IConfiguration configuration,
        ILogger<QdrantClientAdapter> logger,
        Func<string, int, bool, QdrantClient>? clientFactory = null)
    {
        _logger = logger;
        var qdrantUrl = configuration["QDRANT_URL"] ?? "http://localhost:6333";

        if (!Uri.TryCreate(qdrantUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid QDRANT_URL value: {qdrantUrl}");
        }

        var host = uri.Host;
        var useHttps = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var grpcPort = ResolveGrpcPort(configuration, uri, useHttps, logger);

        var factory = clientFactory ?? ((h, p, https) => new QdrantClient(h, p, https));
        _client = factory(host, grpcPort, useHttps);
        _logger.LogInformation("Qdrant client initialized for {Host}:{Port} (HTTPS: {UseHttps})", host, grpcPort, useHttps);
    }

    private static int ResolveGrpcPort(
        IConfiguration configuration,
        Uri uri,
        bool useHttps,
        ILogger logger)
    {
        var grpcPortSetting = configuration["QDRANT_GRPC_PORT"];
        if (!string.IsNullOrWhiteSpace(grpcPortSetting))
        {
            if (int.TryParse(grpcPortSetting, out var explicitPort) && explicitPort > 0)
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

        return useHttps ? 6334 : 6334;
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
}
