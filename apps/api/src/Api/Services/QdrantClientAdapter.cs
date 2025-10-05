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

    public QdrantClientAdapter(IConfiguration configuration, ILogger<QdrantClientAdapter> logger)
    {
        _logger = logger;
        var qdrantUrl = configuration["QDRANT_URL"] ?? "http://localhost:6333";

        var uri = new Uri(qdrantUrl);
        var host = uri.Host;
        var grpcPort = 6334;
        var useHttps = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

        _client = new QdrantClient(host, grpcPort, useHttps);
        _logger.LogInformation("Qdrant client initialized for {Host}:{Port} (HTTPS: {UseHttps})", host, grpcPort, useHttps);
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
