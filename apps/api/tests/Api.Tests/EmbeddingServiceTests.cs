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
        // Configure OpenAI provider for existing tests (they use OpenAI-compatible API)
        _configMock.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("openai");
        _configMock.Setup(c => c["OPENAI_API_KEY"]).Returns("test-api-key");
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
        Assert.Equal("No embeddings returned from OpenAI", result.ErrorMessage);
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
        // Arrange - OpenAI provider without API key
        var configWithoutKey = new Mock<IConfiguration>();
        configWithoutKey.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("openai");
        configWithoutKey.Setup(c => c["OPENAI_API_KEY"]).Returns((string?)null);

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

    #region BDD Scenarios - TEST-02: Additional Coverage Tests

    /// <summary>
    /// BDD Scenario: Ollama provider generates embeddings successfully
    /// Given: EMBEDDING_PROVIDER is set to "ollama"
    /// When: GenerateEmbeddingsAsync is called with 3 texts
    /// Then: Makes 3 sequential POST requests to /api/embeddings and returns 3 embeddings
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaProvider_ReturnsMultipleEmbeddings()
    {
        // Arrange - Ollama provider configuration
        var ollamaConfig = new Mock<IConfiguration>();
        ollamaConfig.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("ollama");
        ollamaConfig.Setup(c => c["OLLAMA_URL"]).Returns("http://localhost:11434");
        ollamaConfig.Setup(c => c["EMBEDDING_MODEL"]).Returns("nomic-embed-text");

        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/embeddings"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var ollamaResponse = JsonSerializer.Serialize(new
                {
                    embedding = Enumerable.Repeat(0.1f, 768).ToArray() // Ollama nomic-embed-text is 768 dims
                });
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(ollamaResponse)
                };
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Ollama")).Returns(httpClient);

        var service = new EmbeddingService(httpClientFactoryMock.Object, ollamaConfig.Object, _loggerMock.Object);

        // Act
        var texts = new List<string> { "text 1", "text 2", "text 3" };
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert - Ollama processes one text at a time
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(3, result.Embeddings.Count);
        Assert.Equal(3, callCount); // Verify 3 separate API calls
        Assert.All(result.Embeddings, embedding => Assert.Equal(768, embedding.Length));
    }

    /// <summary>
    /// BDD Scenario: Ollama provider handles API error gracefully
    /// Given: EMBEDDING_PROVIDER is "ollama" and Ollama API returns 500 Internal Server Error
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Logs error with status code and body, returns failure result
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaApiError_LogsErrorAndReturnsFailure()
    {
        // Arrange - Ollama returns error
        var ollamaConfig = new Mock<IConfiguration>();
        ollamaConfig.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("ollama");
        ollamaConfig.Setup(c => c["OLLAMA_URL"]).Returns("http://localhost:11434");

        var errorBody = "{\"error\": \"Model not found\"}";
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent(errorBody)
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Ollama")).Returns(httpClient);

        var mockLogger = new Mock<ILogger<EmbeddingService>>();
        var service = new EmbeddingService(httpClientFactoryMock.Object, ollamaConfig.Object, mockLogger.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert - Failure result
        Assert.False(result.Success);
        Assert.Contains("API error", result.ErrorMessage);
        Assert.Contains("500", result.ErrorMessage);
        Assert.Empty(result.Embeddings);

        // Assert - Error was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Ollama embeddings API error") &&
                    state.ToString()!.Contains("500") &&
                    state.ToString()!.Contains(errorBody)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// BDD Scenario: Ollama provider handles empty embedding response
    /// Given: EMBEDDING_PROVIDER is "ollama" and Ollama returns empty embedding array
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Returns failure with "No embedding returned from Ollama"
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaEmptyEmbedding_ReturnsFailure()
    {
        // Arrange - Ollama returns empty embedding
        var ollamaConfig = new Mock<IConfiguration>();
        ollamaConfig.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("ollama");
        ollamaConfig.Setup(c => c["OLLAMA_URL"]).Returns("http://localhost:11434");

        var emptyEmbeddingResponse = JsonSerializer.Serialize(new
        {
            embedding = Array.Empty<float>()
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(emptyEmbeddingResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Ollama")).Returns(httpClient);

        var service = new EmbeddingService(httpClientFactoryMock.Object, ollamaConfig.Object, _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No embedding returned from Ollama", result.ErrorMessage);
        Assert.Empty(result.Embeddings);
    }

    /// <summary>
    /// BDD Scenario: Ollama provider handles null embedding response
    /// Given: EMBEDDING_PROVIDER is "ollama" and Ollama returns null embedding
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Returns failure with "No embedding returned from Ollama"
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_OllamaNullEmbedding_ReturnsFailure()
    {
        // Arrange - Ollama returns null embedding
        var ollamaConfig = new Mock<IConfiguration>();
        ollamaConfig.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("ollama");
        ollamaConfig.Setup(c => c["OLLAMA_URL"]).Returns("http://localhost:11434");

        var nullEmbeddingResponse = JsonSerializer.Serialize(new
        {
            embedding = (float[]?)null
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(nullEmbeddingResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Ollama")).Returns(httpClient);

        var service = new EmbeddingService(httpClientFactoryMock.Object, ollamaConfig.Object, _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No embedding returned from Ollama", result.ErrorMessage);
        Assert.Empty(result.Embeddings);
    }

    /// <summary>
    /// BDD Scenario: Constructor with unsupported provider throws exception
    /// Given: EMBEDDING_PROVIDER is set to unsupported value "azure"
    /// When: EmbeddingService is constructed
    /// Then: InvalidOperationException is thrown with message about unsupported provider
    /// </summary>
    [Fact]
    public void Constructor_UnsupportedProvider_ThrowsException()
    {
        // Arrange
        var configWithUnsupportedProvider = new Mock<IConfiguration>();
        configWithUnsupportedProvider.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("azure");

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new EmbeddingService(httpClientFactoryMock.Object, configWithUnsupportedProvider.Object, _loggerMock.Object));

        Assert.Contains("Unsupported embedding provider: azure", exception.Message);
        Assert.Contains("Use 'ollama' or 'openai'", exception.Message);
    }

    /// <summary>
    /// BDD Scenario: OpenAI provider without API key throws exception
    /// Given: EMBEDDING_PROVIDER is "openai" but OPENAI_API_KEY is not configured
    /// When: EmbeddingService is constructed
    /// Then: InvalidOperationException is thrown with message about missing API key
    /// </summary>
    [Fact]
    public void Constructor_OpenAIWithoutApiKey_ThrowsException()
    {
        // Arrange
        var configWithoutApiKey = new Mock<IConfiguration>();
        configWithoutApiKey.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("openai");
        configWithoutApiKey.Setup(c => c["OPENAI_API_KEY"]).Returns((string?)null);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new EmbeddingService(httpClientFactoryMock.Object, configWithoutApiKey.Object, _loggerMock.Object));

        Assert.Contains("OPENAI_API_KEY not configured", exception.Message);
    }

    /// <summary>
    /// BDD Scenario: OpenAI provider handles null data in response
    /// Given: EMBEDDING_PROVIDER is "openai" and API returns response with data: null
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Returns failure with "No embeddings returned from OpenAI"
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_OpenAINullData_ReturnsFailure()
    {
        // Arrange - OpenAI returns null data
        var openaiConfig = new Mock<IConfiguration>();
        openaiConfig.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("openai");
        openaiConfig.Setup(c => c["OPENAI_API_KEY"]).Returns("test-key");

        var nullDataResponse = JsonSerializer.Serialize(new
        {
            data = (object[]?)null,
            model = "text-embedding-3-small"
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(nullDataResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("OpenAI")).Returns(httpClient);

        var service = new EmbeddingService(httpClientFactoryMock.Object, openaiConfig.Object, _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No embeddings returned from OpenAI", result.ErrorMessage);
        Assert.Empty(result.Embeddings);
    }

    /// <summary>
    /// BDD Scenario: OpenAI provider sorts embeddings by index
    /// Given: EMBEDDING_PROVIDER is "openai" and API returns embeddings with indices [2, 0, 1]
    /// When: GenerateEmbeddingsAsync is called with 3 texts
    /// Then: Returned embeddings are sorted by index [0, 1, 2]
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_OpenAIUnorderedIndices_SortsEmbeddingsByIndex()
    {
        // Arrange - OpenAI returns unordered indices
        var openaiConfig = new Mock<IConfiguration>();
        openaiConfig.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("openai");
        openaiConfig.Setup(c => c["OPENAI_API_KEY"]).Returns("test-key");

        // Create embeddings with different values and unordered indices
        var unorderedResponse = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { @object = "embedding", embedding = Enumerable.Repeat(0.3f, 1536).ToArray(), index = 2 },
                new { @object = "embedding", embedding = Enumerable.Repeat(0.1f, 1536).ToArray(), index = 0 },
                new { @object = "embedding", embedding = Enumerable.Repeat(0.2f, 1536).ToArray(), index = 1 }
            },
            model = "text-embedding-3-small"
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(unorderedResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("OpenAI")).Returns(httpClient);

        var service = new EmbeddingService(httpClientFactoryMock.Object, openaiConfig.Object, _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "text 0", "text 1", "text 2" });

        // Assert - Embeddings should be sorted by index
        Assert.True(result.Success);
        Assert.Equal(3, result.Embeddings.Count);
        // Verify order by checking first element value (should be 0.1f for index 0, then 0.2f for index 1, then 0.3f for index 2)
        Assert.Equal(0.1f, result.Embeddings[0][0]);
        Assert.Equal(0.2f, result.Embeddings[1][0]);
        Assert.Equal(0.3f, result.Embeddings[2][0]);
    }

    /// <summary>
    /// BDD Scenario: Provider configuration is case-insensitive
    /// Given: EMBEDDING_PROVIDER is set to "OLLAMA" (uppercase)
    /// When: EmbeddingService is constructed
    /// Then: Ollama provider is configured correctly (case-insensitive check)
    /// </summary>
    [Fact]
    public void Constructor_ProviderNameUppercase_ConfiguresCorrectly()
    {
        // Arrange
        var configWithUppercaseProvider = new Mock<IConfiguration>();
        configWithUppercaseProvider.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("OLLAMA");
        configWithUppercaseProvider.Setup(c => c["OLLAMA_URL"]).Returns("http://localhost:11434");

        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Ollama")).Returns(httpClient);

        // Act - Should not throw exception
        var service = new EmbeddingService(httpClientFactoryMock.Object, configWithUppercaseProvider.Object, _loggerMock.Object);

        // Assert - Service was created successfully (implicit assertion)
        Assert.NotNull(service);
    }

    /// <summary>
    /// BDD Scenario: Successful embedding generation logs information with count
    /// Given: API returns embeddings successfully
    /// When: GenerateEmbeddingsAsync is called with 2 texts
    /// Then: Information log is written with embedding count (2)
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_Success_LogsInformationWithCount()
    {
        // Arrange - OpenAI successful response
        var openaiConfig = new Mock<IConfiguration>();
        openaiConfig.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("openai");
        openaiConfig.Setup(c => c["OPENAI_API_KEY"]).Returns("test-key");

        var successResponse = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { @object = "embedding", embedding = Enumerable.Repeat(0.1f, 1536).ToArray(), index = 0 },
                new { @object = "embedding", embedding = Enumerable.Repeat(0.2f, 1536).ToArray(), index = 1 }
            },
            model = "text-embedding-3-small"
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(successResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("OpenAI")).Returns(httpClient);

        var mockLogger = new Mock<ILogger<EmbeddingService>>();
        var service = new EmbeddingService(httpClientFactoryMock.Object, openaiConfig.Object, mockLogger.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "text 1", "text 2" });

        // Assert - Success result
        Assert.True(result.Success);
        Assert.Equal(2, result.Embeddings.Count);

        // Assert - Info log with count was written
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Successfully generated 2 embeddings")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
