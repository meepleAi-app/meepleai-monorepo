using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Qdrant.Client.Grpc;
using Xunit;

namespace Api.Tests;

public class QdrantServiceTests
{
    private const string CollectionName = "meepleai_documents";

    private readonly Mock<IQdrantClientAdapter> _clientAdapterMock = new();
    private readonly Mock<ILogger<QdrantService>> _loggerMock = new();
    private readonly QdrantService _sut;

    public QdrantServiceTests()
    {
        _sut = new QdrantService(_clientAdapterMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task EnsureCollectionExistsAsync_WhenCollectionExists_DoesNotCreateCollection()
    {
        _clientAdapterMock
            .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CollectionName });

        await _sut.EnsureCollectionExistsAsync();

        _clientAdapterMock.Verify(x => x.CreateCollectionAsync(It.IsAny<string>(), It.IsAny<VectorParams>(), It.IsAny<CancellationToken>()), Times.Never);
        _clientAdapterMock.Verify(x => x.CreatePayloadIndexAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PayloadSchemaType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureCollectionExistsAsync_WhenCollectionMissing_CreatesCollectionAndIndexes()
    {
        _clientAdapterMock
            .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "another_collection" });

        _clientAdapterMock
            .Setup(x => x.CreateCollectionAsync(CollectionName, It.IsAny<VectorParams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _clientAdapterMock
            .Setup(x => x.CreatePayloadIndexAsync(CollectionName, "game_id", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _clientAdapterMock
            .Setup(x => x.CreatePayloadIndexAsync(CollectionName, "pdf_id", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await _sut.EnsureCollectionExistsAsync();

        _clientAdapterMock.Verify(x => x.CreateCollectionAsync(CollectionName,
            It.Is<VectorParams>(v => v.Size == 1536 && v.Distance == Distance.Cosine),
            It.IsAny<CancellationToken>()), Times.Once);

        _clientAdapterMock.Verify(x => x.CreatePayloadIndexAsync(CollectionName, "game_id", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()), Times.Once);
        _clientAdapterMock.Verify(x => x.CreatePayloadIndexAsync(CollectionName, "pdf_id", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IndexDocumentChunksAsync_WithValidChunks_MapsPayloadAndUpserts()
    {
        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "Chunk 1",
                Embedding = Enumerable.Repeat(0.1f, 1536).ToArray(),
                Page = 2,
                CharStart = 10,
                CharEnd = 30
            }
        };

        IEnumerable<PointStruct>? capturedPoints = null;
        _clientAdapterMock
            .Setup(x => x.UpsertAsync(CollectionName, It.IsAny<IEnumerable<PointStruct>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<PointStruct>, CancellationToken>((_, points, _) => capturedPoints = points.ToList())
            .Returns(Task.CompletedTask);

        var result = await _sut.IndexDocumentChunksAsync("game-1", "pdf-1", chunks);

        Assert.True(result.Success);
        Assert.Equal(1, result.IndexedCount);
        Assert.Null(result.ErrorMessage);

        Assert.NotNull(capturedPoints);
        var point = Assert.Single(capturedPoints!);
        Assert.Equal("game-1", point.Payload["game_id"].StringValue);
        Assert.Equal("pdf-1", point.Payload["pdf_id"].StringValue);
        Assert.Equal(0, point.Payload["chunk_index"].IntegerValue);
        Assert.Equal("Chunk 1", point.Payload["text"].StringValue);
        Assert.Equal(2, point.Payload["page"].IntegerValue);
        Assert.Equal(10, point.Payload["char_start"].IntegerValue);
        Assert.Equal(30, point.Payload["char_end"].IntegerValue);
        Assert.True(point.Payload.ContainsKey("indexed_at"));
    }

    [Fact]
    public async Task IndexDocumentChunksAsync_WhenUpsertThrows_ReturnsFailure()
    {
        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Text = "Chunk",
                Embedding = Enumerable.Repeat(0.1f, 1536).ToArray(),
                Page = 1,
                CharStart = 0,
                CharEnd = 10
            }
        };

        _clientAdapterMock
            .Setup(x => x.UpsertAsync(CollectionName, It.IsAny<IEnumerable<PointStruct>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _sut.IndexDocumentChunksAsync("game-1", "pdf-1", chunks);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("boom", result.ErrorMessage);
        Assert.Equal(0, result.IndexedCount);
    }

    [Fact]
    public async Task IndexDocumentChunksAsync_WithNoChunks_ReturnsFailure()
    {
        var result = await _sut.IndexDocumentChunksAsync("game-1", "pdf-1", new List<DocumentChunk>());

        Assert.False(result.Success);
        Assert.Equal("No chunks to index", result.ErrorMessage);
    }

    [Fact]
    public async Task SearchAsync_WhenClientReturnsResults_MapsPayload()
    {
        var scoredPoint = new ScoredPoint
        {
            Score = 0.9f
        };
        scoredPoint.Payload.Add("text", new Value { StringValue = "Answer" });
        scoredPoint.Payload.Add("pdf_id", new Value { StringValue = "pdf-123" });
        scoredPoint.Payload.Add("page", new Value { IntegerValue = 3 });
        scoredPoint.Payload.Add("chunk_index", new Value { IntegerValue = 2 });

        _clientAdapterMock
            .Setup(x => x.SearchAsync(CollectionName, It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { scoredPoint });

        var embedding = Enumerable.Repeat(0.2f, 1536).ToArray();

        var result = await _sut.SearchAsync("game-1", embedding, limit: 5);

        Assert.True(result.Success);
        var item = Assert.Single(result.Results);
        Assert.Equal(0.9f, item.Score);
        Assert.Equal("Answer", item.Text);
        Assert.Equal("pdf-123", item.PdfId);
        Assert.Equal(3, item.Page);
        Assert.Equal(2, item.ChunkIndex);
    }

    [Fact]
    public async Task SearchAsync_WhenClientThrows_ReturnsFailure()
    {
        _clientAdapterMock
            .Setup(x => x.SearchAsync(CollectionName, It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("search failed"));

        var result = await _sut.SearchAsync("game-1", Enumerable.Repeat(0.1f, 1536).ToArray());

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("search failed", result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WhenClientSucceeds_ReturnsTrue()
    {
        _clientAdapterMock
            .Setup(x => x.DeleteAsync(CollectionName, It.IsAny<Filter>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.DeleteDocumentAsync("pdf-1");

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WhenClientThrows_ReturnsFalse()
    {
        _clientAdapterMock
            .Setup(x => x.DeleteAsync(CollectionName, It.IsAny<Filter>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("delete failed"));

        var result = await _sut.DeleteDocumentAsync("pdf-1");

        Assert.False(result);
    }
}
