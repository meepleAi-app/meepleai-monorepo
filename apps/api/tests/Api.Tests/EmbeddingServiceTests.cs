using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public class EmbeddingServiceTests
{
    private readonly Mock<ILogger<EmbeddingService>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;

    public EmbeddingServiceTests()
    {
        _loggerMock = new Mock<ILogger<EmbeddingService>>();
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["OPENROUTER_API_KEY"]).Returns("test-api-key");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SuccessfulResponse_ReturnsEmbedding()
    {
        // Arrange
        var responseContent = CreateSuccessfulEmbeddingResponse(1);
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);
        var service = CreateEmbeddingService(httpClient);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Single(result.Embeddings);
        Assert.Equal(1536, result.Embeddings[0].Length);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_MultipleTexts_ReturnsMultipleEmbeddings()
    {
        // Arrange
        var texts = new List<string> { "text 1", "text 2", "text 3" };
        var responseContent = CreateSuccessfulEmbeddingResponse(texts.Count);
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);
        var service = CreateEmbeddingService(httpClient);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(3, result.Embeddings.Count);
        Assert.All(result.Embeddings, embedding => Assert.Equal(1536, embedding.Length));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_EmptyList_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var service = CreateEmbeddingService(httpClient);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No texts provided", result.ErrorMessage);
        Assert.Empty(result.Embeddings);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_NullList_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var service = CreateEmbeddingService(httpClient);

        // Act
        var result = await service.GenerateEmbeddingsAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No texts provided", result.ErrorMessage);
        Assert.Empty(result.Embeddings);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ApiError_ReturnsFailure()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, "{\"error\": \"Invalid request\"}");
        var service = CreateEmbeddingService(httpClient);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API error", result.ErrorMessage);
        Assert.Empty(result.Embeddings);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_NetworkError_ReturnsFailure()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(httpClient);

        var service = new EmbeddingService(httpClientFactoryMock.Object, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Network error", result.ErrorMessage!);
        Assert.Empty(result.Embeddings);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_Timeout_ReturnsFailure()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(httpClient);

        var service = new EmbeddingService(httpClientFactoryMock.Object, _configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Request timed out", result.ErrorMessage);
        Assert.Empty(result.Embeddings);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_EmptyResponseData_ReturnsFailure()
    {
        // Arrange
        var responseContent = JsonSerializer.Serialize(new
        {
            data = Array.Empty<object>(),
            model = "openai/text-embedding-3-small"
        });

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);
        var service = CreateEmbeddingService(httpClient);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No embeddings returned from API", result.ErrorMessage);
        Assert.Empty(result.Embeddings);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_CancellationRequested_ThrowsOrReturnsFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, CreateSuccessfulEmbeddingResponse(1));
        var service = CreateEmbeddingService(httpClient);

        // Act & Assert
        // Depending on when cancellation is checked, it might throw or return failure
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "test" }, cts.Token);

        // The service should handle cancellation gracefully
        Assert.False(result.Success);
    }

    [Fact]
    public void Constructor_MissingApiKey_ThrowsException()
    {
        // Arrange
        var configWithoutKey = new Mock<IConfiguration>();
        configWithoutKey.Setup(c => c["OPENROUTER_API_KEY"]).Returns((string?)null);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new EmbeddingService(httpClientFactoryMock.Object, configWithoutKey.Object, _loggerMock.Object));
    }

    // Helper methods

    private EmbeddingService CreateEmbeddingService(HttpClient httpClient)
    {
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(httpClient);

        return new EmbeddingService(httpClientFactoryMock.Object, _configMock.Object, _loggerMock.Object);
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseContent)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
    }

    private string CreateSuccessfulEmbeddingResponse(int count)
    {
        var embeddings = Enumerable.Range(0, count)
            .Select(i => new
            {
                @object = "embedding",
                embedding = Enumerable.Repeat(0.1f, 1536).ToArray(),
                index = i
            })
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            data = embeddings,
            model = "openai/text-embedding-3-small",
            usage = new
            {
                prompt_tokens = 10 * count,
                total_tokens = 10 * count
            }
        });
    }
}
