using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Comprehensive unit tests for EmbeddingService following BDD naming conventions.
/// Target: ≥95% code coverage for TEST-02-P3.
/// Covers constructor validation, provider-specific logic, edge cases, and error handling.
/// </summary>
public class EmbeddingServiceComprehensiveTests
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<EmbeddingService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public EmbeddingServiceComprehensiveTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<EmbeddingService>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenOllamaProvider_ShouldConfigureCorrectly()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            ["EMBEDDING_PROVIDER"] = "ollama",
            ["OLLAMA_URL"] = "http://localhost:11434",
            ["EMBEDDING_MODEL"] = "nomic-embed-text"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Ollama"))
            .Returns(httpClient);

        // Act
        var service = new EmbeddingService(_httpClientFactoryMock.Object, config, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        _httpClientFactoryMock.Verify(f => f.CreateClient("Ollama"), Times.Once);
    }

    [Fact]
    public void Constructor_WhenOpenAIProvider_ShouldConfigureCorrectly()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            ["EMBEDDING_PROVIDER"] = "openai",
            ["OPENAI_API_KEY"] = "sk-test-key",
            ["EMBEDDING_MODEL"] = "text-embedding-3-small"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("OpenRouter"))
            .Returns(httpClient);

        // Act
        var service = new EmbeddingService(_httpClientFactoryMock.Object, config, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        _httpClientFactoryMock.Verify(f => f.CreateClient("OpenRouter"), Times.Once);
    }

    [Fact]
    public void Constructor_WhenNoProviderConfigured_ShouldDefaultToOllama()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Ollama"))
            .Returns(httpClient);

        // Act
        var service = new EmbeddingService(_httpClientFactoryMock.Object, config, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        _httpClientFactoryMock.Verify(f => f.CreateClient("Ollama"), Times.Once);
    }

    [Fact]
    public void Constructor_WhenOpenAIProviderWithoutApiKey_ShouldThrow()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            ["EMBEDDING_PROVIDER"] = "openai"
            // No OPENAI_API_KEY
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("OpenRouter"))
            .Returns(httpClient);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new EmbeddingService(_httpClientFactoryMock.Object, config, _mockLogger.Object));

        Assert.Contains("OPENAI_API_KEY not configured", ex.Message);
    }

    [Fact]
    public void Constructor_WhenInvalidProvider_ShouldThrow()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            ["EMBEDDING_PROVIDER"] = "invalid-provider"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new EmbeddingService(_httpClientFactoryMock.Object, config, _mockLogger.Object));

        Assert.Contains("Unsupported embedding provider", ex.Message);
        Assert.Contains("invalid-provider", ex.Message);
    }

    [Fact]
    public void Constructor_WhenOllamaProviderWithCustomUrl_ShouldUseCustomUrl()
    {
        // Arrange
        var customUrl = "http://custom-ollama:8080";
        var configDict = new Dictionary<string, string?>
        {
            ["EMBEDDING_PROVIDER"] = "ollama",
            ["OLLAMA_URL"] = customUrl
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var httpClient = new HttpClient();
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Ollama"))
            .Returns(httpClient);

        // Act
        var service = new EmbeddingService(_httpClientFactoryMock.Object, config, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region GenerateEmbeddingsAsync - Ollama Provider Tests

    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaProvider_WhenLargeBatch_ShouldProcessAllSequentially()
    {
        // Arrange
        var texts = Enumerable.Range(1, 100).Select(i => $"Text {i}").ToList();
        var service = CreateOllamaService(createSuccessResponse: true);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(100, result.Embeddings.Count);
        Assert.All(result.Embeddings, emb => Assert.Equal(768, emb.Length));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaProvider_WhenSingleText_ShouldSucceed()
    {
        // Arrange
        var texts = new List<string> { "Single text" };
        var service = CreateOllamaService(createSuccessResponse: true);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Embeddings);
        Assert.Equal(768, result.Embeddings[0].Length);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaProvider_WhenApiReturnsZeroLengthEmbedding_ShouldReturnFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var service = CreateOllamaService(zeroLengthEmbedding: true);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No embedding returned from Ollama", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaProvider_WhenCancellationRequested_ShouldReturnFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var service = CreateOllamaService(throwCancellation: true);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage);
    }

    #endregion

    #region GenerateEmbeddingsAsync - OpenAI Provider Tests

    [Fact]
    public async Task GenerateEmbeddingsAsync_OpenAIProvider_WhenLargeBatch_ShouldSucceed()
    {
        // Arrange
        var texts = Enumerable.Range(1, 100).Select(i => $"Text {i}").ToList();
        var service = CreateOpenAIService(createSuccessResponse: true, count: 100);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(100, result.Embeddings.Count);
        Assert.All(result.Embeddings, emb => Assert.Equal(1536, emb.Length));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OpenAIProvider_WhenEmptyDataArray_ShouldReturnFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var service = CreateOpenAIService(emptyDataArray: true);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No embeddings returned from OpenAI", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OpenAIProvider_WhenCancellationRequested_ShouldReturnFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var service = CreateOpenAIService(throwCancellation: true);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage);
    }

    #endregion

    #region GenerateEmbeddingAsync (Single Text) Tests

    [Fact]
    public async Task GenerateEmbeddingAsync_OllamaProvider_WhenSuccessful_ShouldReturnSingleEmbedding()
    {
        // Arrange
        var service = CreateOllamaService(createSuccessResponse: true);

        // Act
        var result = await service.GenerateEmbeddingAsync("Test text");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Embeddings);
        Assert.Equal(768, result.Embeddings[0].Length);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_OpenAIProvider_WhenSuccessful_ShouldReturnSingleEmbedding()
    {
        // Arrange
        var service = CreateOpenAIService(createSuccessResponse: true, count: 1);

        // Act
        var result = await service.GenerateEmbeddingAsync("Test text");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Embeddings);
        Assert.Equal(1536, result.Embeddings[0].Length);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenEmptyTextsList_ShouldReturnFailure()
    {
        // Arrange
        var service = CreateOllamaService(createSuccessResponse: true);
        var texts = new List<string>();

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No texts provided", result.ErrorMessage);
        result.Embeddings.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenNullTextsList_ShouldReturnFailure()
    {
        // Arrange
        var service = CreateOllamaService(createSuccessResponse: true);

        // Act
        var result = await service.GenerateEmbeddingsAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No texts provided", result.ErrorMessage);
        result.Embeddings.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaProvider_WhenHttpError_ShouldReturnFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var service = CreateOllamaService(statusCode: HttpStatusCode.InternalServerError);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API error: 500", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OpenAIProvider_WhenHttpError_ShouldReturnFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var service = CreateOpenAIService(statusCode: HttpStatusCode.Unauthorized);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API error: 401", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaProvider_WhenExceptionThrown_ShouldCatchAndReturnFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var service = CreateOllamaService(throwException: true);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("error", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_OpenAIProvider_WhenExceptionThrown_ShouldCatchAndReturnFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var service = CreateOpenAIService(throwException: true);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("error", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentEmbeddings_WhenMultipleThreads_ShouldHandleCorrectly()
    {
        // Arrange
        var service = CreateOllamaService(createSuccessResponse: true);
        var texts = new List<string> { "Text 1", "Text 2", "Text 3" };

        // Act
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => service.GenerateEmbeddingsAsync(texts))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.True(r.Success));
        Assert.All(results, r => Assert.Equal(3, r.Embeddings.Count));
    }

    #endregion

    #region Helper Methods

    private EmbeddingService CreateOllamaService(
        bool createSuccessResponse = false,
        bool zeroLengthEmbedding = false,
        bool throwCancellation = false,
        bool throwException = false,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["EMBEDDING_PROVIDER"] = "ollama",
            ["OLLAMA_URL"] = "http://localhost:11434",
            ["EMBEDDING_MODEL"] = "nomic-embed-text"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                if (throwCancellation)
                {
                    throw new TaskCanceledException("Request timeout");
                }

                if (throwException)
                {
                    throw new InvalidOperationException("Network error");
                }

                if (createSuccessResponse)
                {
                    var embedding = zeroLengthEmbedding ? Array.Empty<float>() : CreateRandomEmbedding(768);
                    var response = new
                    {
                        embedding = embedding
                    };
                    var jsonResponse = JsonSerializer.Serialize(response);

                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = statusCode,
                        Content = new StringContent(jsonResponse)
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent("{}")
                });
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Ollama"))
            .Returns(httpClient);

        return new EmbeddingService(_httpClientFactoryMock.Object, config, _mockLogger.Object);
    }

    private EmbeddingService CreateOpenAIService(
        bool createSuccessResponse = false,
        int count = 1,
        bool emptyDataArray = false,
        bool throwCancellation = false,
        bool throwException = false,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["EMBEDDING_PROVIDER"] = "openai",
            ["OPENAI_API_KEY"] = "sk-test-key",
            ["EMBEDDING_MODEL"] = "text-embedding-3-small"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                if (throwCancellation)
                {
                    throw new TaskCanceledException("Request timeout");
                }

                if (throwException)
                {
                    throw new InvalidOperationException("Network error");
                }

                if (createSuccessResponse)
                {
                    var data = emptyDataArray
                        ? new List<object>()
                        : Enumerable.Range(0, count).Select(i => new
                        {
                            @object = "embedding",
                            embedding = CreateRandomEmbedding(1536),
                            index = i
                        }).ToList<object>();

                    var response = new
                    {
                        data = data,
                        model = "text-embedding-3-small",
                        usage = new
                        {
                            prompt_tokens = count * 10,
                            total_tokens = count * 10
                        }
                    };
                    var jsonResponse = JsonSerializer.Serialize(response);

                    return Task.FromResult(new HttpResponseMessage
                    {
                        StatusCode = statusCode,
                        Content = new StringContent(jsonResponse)
                    });
                }

                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent("{}")
                });
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("OpenRouter"))
            .Returns(httpClient);

        return new EmbeddingService(_httpClientFactoryMock.Object, config, _mockLogger.Object);
    }

    private static float[] CreateRandomEmbedding(int dimensions)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var embedding = new float[dimensions];

        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)random.NextDouble();
        }

        return embedding;
    }

    #endregion
}
