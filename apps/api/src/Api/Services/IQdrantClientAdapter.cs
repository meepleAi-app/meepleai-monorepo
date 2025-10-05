using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Qdrant.Client.Grpc;

namespace Api.Services;

public interface IQdrantClientAdapter
{
    Task<IEnumerable<string>> ListCollectionsAsync(CancellationToken cancellationToken = default);
    Task CreateCollectionAsync(
        string collectionName,
        VectorParams vectorsConfig,
        CancellationToken cancellationToken = default);
    Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken cancellationToken = default);
    Task UpsertAsync(
        string collectionName,
        IEnumerable<PointStruct> points,
        CancellationToken cancellationToken = default);
    Task<IEnumerable<ScoredPoint>> SearchAsync(
        string collectionName,
        float[] vector,
        Filter? filter = default,
        ulong? limit = null,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(
        string collectionName,
        Filter filter,
        CancellationToken cancellationToken = default);
}
