using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.External.Reranking;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Reranking;

/// <summary>
/// Unit tests for CrossEncoderRerankerClient.
/// ADR-016 Phase 4: Cross-encoder reranking HTTP client.
/// </summary>
public class CrossEncoderRerankerClientTests
{
    private readonly Mock<ILogger<CrossEncoderRerankerClient>> _loggerMock;
    private readonly RerankerClientOptions _options;

    public CrossEncoderRerankerClientTests()
    {
        _loggerMock = new Mock<ILogger<CrossEncoderRerankerClient>>();
        _options = new RerankerClientOptions
        {
            BaseUrl = "http://localhost:8003",
            TimeoutMs = 5000
        };
    }

    private CrossEncoderRerankerClient CreateClient(Mock<HttpMessageHandler> handlerMock)
    {
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri(_options.BaseUrl)
        };

        // Constructor order: HttpClient, ILogger, IOptions
        return new CrossEncoderRerankerClient(
            httpClient,
            _loggerMock.Object,
            Options.Create(_options));
    }

    [Fact]
    public async Task RerankAsync_WithValidChunks_ReturnsRankedResults()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var responseContent = new
        {
            results = new[]
            {
                new { id = "chunk-1", content = "Best match", original_score = 0.9, rerank_score = 0.95, metadata = new Dictionary<string, object>() },
                new { id = "chunk-2", content = "Second match", original_score = 0.8, rerank_score = 0.85, metadata = new Dictionary<string, object>() }
            },
            model = "BAAI/bge-reranker-v2-m3",
            processing_time_ms = 150.0
        };

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(responseContent),
                    Encoding.UTF8,
                    "application/json")
            });

        var client = CreateClient(handlerMock);
        // RerankChunk has 4 params: Id, Content, OriginalScore (double), Metadata
        var chunks = new List<RerankChunk>
        {
            new("chunk-1", "Best match", 0.9, null),
            new("chunk-2", "Second match", 0.8, null),
            new("chunk-3", "Third match", 0.7, null)
        };

        // Act
        var result = await client.RerankAsync("test query", chunks, 2);

        // Assert
        result.Should().NotBeNull();
        result.Chunks.Should().HaveCount(2);
        result.Chunks[0].Id.Should().Be("chunk-1");
        result.Chunks[0].RerankScore.Should().BeApproximately(0.95, 0.001);
        result.Model.Should().Be("BAAI/bge-reranker-v2-m3");
    }

    [Fact]
    public async Task RerankAsync_WithEmptyChunks_ReturnsEmptyResult()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var client = CreateClient(handlerMock);

        // Act
        var result = await client.RerankAsync("test query", new List<RerankChunk>(), 5);

        // Assert
        result.Chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task RerankAsync_WithNullQuery_ThrowsArgumentException()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var client = CreateClient(handlerMock);

        // Act
        var act = () => client.RerankAsync(null!, new List<RerankChunk>(), 5);

        // Assert - throws ArgumentException for empty/whitespace
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RerankAsync_WithServiceError_ThrowsRerankerServiceException()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Internal Server Error")
            });

        var client = CreateClient(handlerMock);
        var chunks = new List<RerankChunk>
        {
            new("chunk-1", "Test content", 0.9, null)
        };

        // Act
        var act = () => client.RerankAsync("test query", chunks, 5);

        // Assert - client wraps HTTP errors in RerankerServiceException
        await act.Should().ThrowAsync<RerankerServiceException>();
    }

    [Fact]
    public async Task RerankAsync_WithTimeout_ThrowsException()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        // Create a different CancellationToken to simulate internal timeout (not user cancellation)
        // The client catches TaskCanceledException only when ex.CancellationToken != cancellationToken
        using var timeoutCts = new CancellationTokenSource();
        var timeoutException = new TaskCanceledException("Request timed out", null, timeoutCts.Token);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(timeoutException);

        var client = CreateClient(handlerMock);
        var chunks = new List<RerankChunk>
        {
            new("chunk-1", "Test content", 0.9, null)
        };

        // Act - call with default token (CancellationToken.None)
        var act = () => client.RerankAsync("test query", chunks, 5);

        // Assert - TaskCanceledException wrapped in RerankerServiceException with timeout message
        await act.Should().ThrowAsync<RerankerServiceException>()
            .WithMessage("*timed out*");
    }

    [Fact]
    public async Task IsHealthyAsync_WhenServiceResponds_ReturnsTrue()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.Contains("/health")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"healthy\",\"model_loaded\":true}")
            });

        var client = CreateClient(handlerMock);

        // Act
        var result = await client.IsHealthyAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenServiceUnreachable_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = CreateClient(handlerMock);

        // Act
        var result = await client.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenServiceReturnsError_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            });

        var client = CreateClient(handlerMock);

        // Act
        var result = await client.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RerankAsync_PreservesMetadata_InResults()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var metadata = new Dictionary<string, object>
        {
            ["page_number"] = 5,
            ["pdf_document_id"] = Guid.NewGuid().ToString()
        };

        var responseContent = new
        {
            results = new[]
            {
                new
                {
                    id = "chunk-1",
                    content = "Test content",
                    original_score = 0.9,
                    rerank_score = 0.95,
                    metadata = metadata
                }
            },
            model = "BAAI/bge-reranker-v2-m3",
            processing_time_ms = 100.0
        };

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(responseContent),
                    Encoding.UTF8,
                    "application/json")
            });

        var client = CreateClient(handlerMock);
        var chunks = new List<RerankChunk>
        {
            new("chunk-1", "Test content", 0.9, metadata)
        };

        // Act
        var result = await client.RerankAsync("test query", chunks, 5);

        // Assert
        result.Chunks[0].Metadata.Should().NotBeNull();
        result.Chunks[0].Metadata!["page_number"].Should().NotBeNull();
    }
}
