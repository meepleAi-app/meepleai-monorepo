using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// AI-09: Multilingual embedding tests for language-aware embedding generation
/// Tests the fallback chain: Local Python service → Ollama → OpenRouter
/// </summary>
public class EmbeddingServiceMultilingualTests
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<EmbeddingService>> _loggerMock;

    public EmbeddingServiceMultilingualTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger<EmbeddingService>>();
    }

    #region Language-Aware Embedding Generation

    /// <summary>
    /// BDD Scenario: Generate embeddings for Italian text
    /// Given: Language is "it" and local service is unavailable
    /// When: GenerateEmbeddingsAsync is called with Italian text
    /// Then: Falls back to Ollama and returns embeddings with language context
    /// </summary>
    [Theory]
    [InlineData("en", "English text for embedding")]
    [InlineData("it", "Testo italiano per embedding")]
    [InlineData("de", "Deutscher Text für Einbettung")]
    [InlineData("fr", "Texte français pour intégration")]
    [InlineData("es", "Texto español para incrustar")]
    public async Task GenerateEmbeddingsAsync_WithValidLanguage_ReturnsEmbeddings(string language, string text)
    {
        // Arrange - Ollama provider (fallback)
        var config = CreateOllamaConfig(localEmbeddingEnabled: false);
        var httpClient = CreateMockOllamaClient(HttpStatusCode.OK, CreateOllamaEmbeddingResponse());
        var service = CreateService(config, ollamaClient: httpClient);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { text }, language);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Embeddings.Should().ContainSingle()
            .Which.Length.Should().Be(768); // Ollama nomic-embed-text dimension
    }

    /// <summary>
    /// BDD Scenario: Generate embeddings with single text and language
    /// Given: Language is "it" and local service is unavailable
    /// When: GenerateEmbeddingAsync (singular) is called
    /// Then: Returns single embedding using language-aware method
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingAsync_WithLanguage_ReturnsSingleEmbedding()
    {
        // Arrange
        var config = CreateOllamaConfig(localEmbeddingEnabled: false);
        var httpClient = CreateMockOllamaClient(HttpStatusCode.OK, CreateOllamaEmbeddingResponse());
        var service = CreateService(config, ollamaClient: httpClient);

        // Act
        var result = await service.GenerateEmbeddingAsync("Testo italiano", "it");

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().ContainSingle()
            .Which.Length.Should().Be(768);
    }

    /// <summary>
    /// BDD Scenario: Invalid language code defaults to English
    /// Given: Language is "ja" (unsupported)
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Logs warning, defaults to "en", and generates embeddings
    /// </summary>
    [Theory]
    [InlineData("ja")]
    [InlineData("zh")]
    [InlineData("ru")]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public async Task GenerateEmbeddingsAsync_WithUnsupportedLanguage_DefaultsToEnglish(string? language)
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EmbeddingService>>();
        var config = CreateOllamaConfig(localEmbeddingEnabled: false);
        var httpClient = CreateMockOllamaClient(HttpStatusCode.OK, CreateOllamaEmbeddingResponse());
        var service = CreateService(config, ollamaClient: httpClient, logger: mockLogger.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "test" }, language!);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().ContainSingle();

        // Verify warning was logged for non-empty/non-null unsupported languages
        if (!string.IsNullOrWhiteSpace(language))
        {
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("Unsupported language code") &&
                        state.ToString()!.Contains(language) &&
                        state.ToString()!.Contains("falling back to 'en'")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }

    #endregion

    #region Local Embedding Service - Success Scenarios

    /// <summary>
    /// BDD Scenario: Local embedding service generates embeddings successfully
    /// Given: Local service is configured and available
    /// When: GenerateEmbeddingsAsync is called with Italian text
    /// Then: Uses local service (no fallback) and returns 1024-dim embeddings
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_LocalServiceAvailable_UsesLocalService()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EmbeddingService>>();
        var config = CreateOllamaConfig(localEmbeddingEnabled: true, localEmbeddingUrl: "http://localhost:5001");

        var localResponse = CreateLocalEmbeddingResponse(count: 2, dimension: 1024);
        var localClient = CreateMockHttpClient(HttpStatusCode.OK, localResponse, "/embeddings");

        var service = CreateService(config, localEmbeddingClient: localClient, logger: mockLogger.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "text 1", "text 2" }, "it");

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Count.Should().Be(2);
        result.Embeddings.Should().OnlyContain(emb => emb.Length == 1024); // Local service dimension

        // Verify success log
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Successfully generated embeddings using local service") &&
                    state.ToString()!.Contains("language it")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// BDD Scenario: Local service sends language parameter in request
    /// Given: Local service is configured
    /// When: GenerateEmbeddingsAsync is called with language "de"
    /// Then: Request body includes language field set to "de"
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_LocalService_SendsLanguageInRequest()
    {
        // Arrange
        var config = CreateOllamaConfig(localEmbeddingEnabled: true, localEmbeddingUrl: "http://localhost:5001");

        string? capturedRequestBody = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.PathAndQuery == "/embeddings" &&
                    req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                capturedRequestBody = request.Content!.ReadAsStringAsync().Result;
                var response = CreateLocalEmbeddingResponse(count: 1, dimension: 1024);
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(response)
                };
            });

        var localClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var service = CreateService(config, localEmbeddingClient: localClient);

        // Act
        await service.GenerateEmbeddingsAsync(new List<string> { "German text" }, "de");

        // Assert
        capturedRequestBody.Should().NotBeNull();
        var requestData = JsonSerializer.Deserialize<JsonElement>(capturedRequestBody);
        requestData.GetProperty("language").GetString().Should().Be("de");
        requestData.GetProperty("texts").GetArrayLength().Should().Be(1);
    }

    #endregion

    #region Local Embedding Service - Failure and Fallback

    /// <summary>
    /// BDD Scenario: Local service unavailable, falls back to Ollama
    /// Given: Local service is configured but returns 500 error
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Logs warning, falls back to Ollama, returns 768-dim embeddings
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_LocalServiceError_FallsBackToOllama()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EmbeddingService>>();
        var config = CreateOllamaConfig(localEmbeddingEnabled: true, localEmbeddingUrl: "http://localhost:5001");

        var localClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "{\"error\":\"Model not loaded\"}");
        var ollamaClient = CreateMockOllamaClient(HttpStatusCode.OK, CreateOllamaEmbeddingResponse());

        var service = CreateService(config, localEmbeddingClient: localClient, ollamaClient: ollamaClient, logger: mockLogger.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "test" }, "it");

        // Assert - Fallback succeeded
        result.Success.Should().BeTrue();
        result.Embeddings.Should().ContainSingle()
            .Which.Length.Should().Be(768); // Ollama dimension, not local (1024)

        // Verify warning logged about local service failure
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Local embedding service failed") &&
                    state.ToString()!.Contains("falling back to ollama")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// BDD Scenario: Local service unavailable (network error), falls back to Ollama
    /// Given: Local service throws HttpRequestException
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Logs warning about unavailability, falls back to Ollama successfully
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_LocalServiceUnavailable_FallsBackToOllama()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EmbeddingService>>();
        var config = CreateOllamaConfig(localEmbeddingEnabled: true, localEmbeddingUrl: "http://localhost:5001");

        // Local service throws network error
        var localHandlerMock = new Mock<HttpMessageHandler>();
        localHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var localClient = new HttpClient(localHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var ollamaClient = CreateMockOllamaClient(HttpStatusCode.OK, CreateOllamaEmbeddingResponse());

        var service = CreateService(config, localEmbeddingClient: localClient, ollamaClient: ollamaClient, logger: mockLogger.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "test" }, "fr");

        // Assert - Fallback to Ollama succeeded
        result.Success.Should().BeTrue();
        result.Embeddings[0].Length.Should().Be(768); // Ollama dimension

        // Verify warning logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Local embedding service unavailable")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// BDD Scenario: Local service returns empty embeddings, falls back to Ollama
    /// Given: Local service returns success but empty embeddings array
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Falls back to Ollama and returns valid embeddings
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_LocalServiceEmptyResponse_FallsBackToOllama()
    {
        // Arrange
        var config = CreateOllamaConfig(localEmbeddingEnabled: true, localEmbeddingUrl: "http://localhost:5001");

        var emptyResponse = JsonSerializer.Serialize(new
        {
            embeddings = Array.Empty<float[]>(),
            model = "multilingual-e5-large",
            dimension = 1024,
            count = 0
        });

        var localClient = CreateMockHttpClient(HttpStatusCode.OK, emptyResponse);
        var ollamaClient = CreateMockOllamaClient(HttpStatusCode.OK, CreateOllamaEmbeddingResponse());

        var service = CreateService(config, localEmbeddingClient: localClient, ollamaClient: ollamaClient);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "test" }, "es");

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings[0].Length.Should().Be(768); // Ollama fallback
    }

    #endregion

    #region Fallback Chain with OpenRouter

    /// <summary>
    /// BDD Scenario: Local service fails, falls back to OpenRouter (not Ollama)
    /// Given: Provider is "openai" (OpenRouter), local service enabled but fails
    /// When: GenerateEmbeddingsAsync is called with language
    /// Then: Falls back to OpenRouter and generates embeddings
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_OpenRouterProvider_LocalFailure_FallsBackToOpenRouter()
    {
        // Arrange
        var config = CreateOpenRouterConfig(localEmbeddingEnabled: true, localEmbeddingUrl: "http://localhost:5001");

        var localClient = CreateMockHttpClient(HttpStatusCode.ServiceUnavailable, "{}", "/embeddings");
        var openRouterClient = CreateMockOpenRouterClient(HttpStatusCode.OK, CreateOpenAIEmbeddingResponse(1));

        var service = CreateService(config, localEmbeddingClient: localClient, openRouterClient: openRouterClient);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "test" }, "it");

        // Assert
        result.Success.Should().BeTrue($"Expected success but got failure. Error: {result.ErrorMessage}");
        result.Embeddings.Should().ContainSingle()
            .Which.Length.Should().Be(1536); // OpenRouter dimension
    }

    /// <summary>
    /// BDD Scenario: Fallback disabled, local service failure causes overall failure
    /// Given: EMBEDDING_FALLBACK_ENABLED is false and local service is configured but fails
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Does NOT attempt fallback, uses Ollama directly (as if local service didn't exist)
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_FallbackDisabled_DoesNotUseLocalService()
    {
        // Arrange
        var config = CreateOllamaConfig(localEmbeddingEnabled: false, localEmbeddingUrl: "http://localhost:5001");

        // Local service would fail if called, but it shouldn't be called
        var localHandlerMock = new Mock<HttpMessageHandler>();
        localHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Local service should not be called"));

        var localClient = new HttpClient(localHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var ollamaClient = CreateMockOllamaClient(HttpStatusCode.OK, CreateOllamaEmbeddingResponse());

        var service = CreateService(config, localEmbeddingClient: localClient, ollamaClient: ollamaClient);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "test" }, "de");

        // Assert - Should use Ollama directly, not attempt local service
        result.Success.Should().BeTrue();
        result.Embeddings[0].Length.Should().Be(768); // Ollama dimension
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// BDD Scenario: Empty text list with language parameter
    /// Given: Empty texts list is provided with valid language
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Returns failure immediately without calling any service
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_EmptyTextsWithLanguage_ReturnsFailure()
    {
        // Arrange
        var config = CreateOllamaConfig(localEmbeddingEnabled: false);
        var service = CreateService(config);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string>(), "it");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No texts provided");
        result.Embeddings.Should().BeEmpty();
    }

    /// <summary>
    /// BDD Scenario: Null text list with language parameter
    /// Given: Null texts list is provided
    /// When: GenerateEmbeddingsAsync is called
    /// Then: Returns failure immediately
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_NullTextsWithLanguage_ReturnsFailure()
    {
        // Arrange
        var config = CreateOllamaConfig(localEmbeddingEnabled: false);
        var service = CreateService(config);

        // Act
        var result = await service.GenerateEmbeddingsAsync(null!, "fr");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No texts provided");
    }

    /// <summary>
    /// BDD Scenario: Cancellation during language-aware embedding generation
    /// Given: CancellationToken is cancelled
    /// When: GenerateEmbeddingsAsync with language is called
    /// Then: Returns timeout failure result
    /// </summary>
    [Fact]
    public async Task GenerateEmbeddingsAsync_WithLanguage_CancellationRequested_ReturnsFailure()
    {
        // Arrange
        var config = CreateOllamaConfig(localEmbeddingEnabled: false);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = CreateService(config);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string> { "test" }, "en", cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Request timed out");
    }

    #endregion

    #region Helper Methods

    private Mock<IConfiguration> CreateOllamaConfig(bool localEmbeddingEnabled, string? localEmbeddingUrl = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("ollama");
        config.Setup(c => c["OLLAMA_URL"]).Returns("http://localhost:11434");
        config.Setup(c => c["EMBEDDING_MODEL"]).Returns("nomic-embed-text");
        config.Setup(c => c["EMBEDDING_FALLBACK_ENABLED"]).Returns(localEmbeddingEnabled.ToString());
        config.Setup(c => c["LOCAL_EMBEDDING_URL"]).Returns(localEmbeddingUrl);
        return config;
    }

    private Mock<IConfiguration> CreateOpenRouterConfig(bool localEmbeddingEnabled, string? localEmbeddingUrl = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["EMBEDDING_PROVIDER"]).Returns("openai");
        config.Setup(c => c["OPENAI_API_KEY"]).Returns("test-key");
        config.Setup(c => c["EMBEDDING_MODEL"]).Returns("text-embedding-3-small");
        config.Setup(c => c["EMBEDDING_FALLBACK_ENABLED"]).Returns(localEmbeddingEnabled.ToString());
        config.Setup(c => c["LOCAL_EMBEDDING_URL"]).Returns(localEmbeddingUrl);
        return config;
    }

    private EmbeddingService CreateService(
        Mock<IConfiguration> config,
        HttpClient? localEmbeddingClient = null,
        HttpClient? ollamaClient = null,
        HttpClient? openRouterClient = null,
        ILogger<EmbeddingService>? logger = null)
    {
        var factoryMock = new Mock<IHttpClientFactory>();

        if (localEmbeddingClient != null)
        {
            factoryMock.Setup(f => f.CreateClient("LocalEmbedding")).Returns(localEmbeddingClient);
        }
        else
        {
            factoryMock.Setup(f => f.CreateClient("LocalEmbedding")).Returns(new HttpClient());
        }

        if (ollamaClient != null)
        {
            factoryMock.Setup(f => f.CreateClient("Ollama")).Returns(ollamaClient);
        }
        else
        {
            factoryMock.Setup(f => f.CreateClient("Ollama")).Returns(new HttpClient { BaseAddress = new Uri("http://localhost:11434") });
        }

        if (openRouterClient != null)
        {
            factoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(openRouterClient);
        }
        else
        {
            factoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(new HttpClient { BaseAddress = new Uri("https://openrouter.ai/api/v1/") });
        }

        return new EmbeddingService(factoryMock.Object, config.Object, logger ?? _loggerMock.Object);
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseContent, string? expectedPath = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>();

        var setup = handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                expectedPath != null
                    ? ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == expectedPath)
                    : ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        setup.ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(responseContent)
        });

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };
    }

    private HttpClient CreateMockOllamaClient(HttpStatusCode statusCode, string responseContent)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/embeddings"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
    }

    private HttpClient CreateMockOpenRouterClient(HttpStatusCode statusCode, string responseContent)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery == "/api/v1/embeddings"),
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

    private string CreateOllamaEmbeddingResponse()
    {
        return JsonSerializer.Serialize(new
        {
            embedding = Enumerable.Repeat(0.1f, 768).ToArray()
        });
    }

    private string CreateLocalEmbeddingResponse(int count, int dimension)
    {
        var embeddings = Enumerable.Range(0, count)
            .Select(_ => Enumerable.Repeat(0.2f, dimension).ToArray())
            .ToList();

        return JsonSerializer.Serialize(new
        {
            embeddings = embeddings,
            model = "multilingual-e5-large",
            dimension = dimension,
            count = count
        });
    }

    private string CreateOpenAIEmbeddingResponse(int count)
    {
        var embeddings = Enumerable.Range(0, count)
            .Select(i => new
            {
                @object = "embedding",
                embedding = Enumerable.Repeat(0.3f, 1536).ToArray(),
                index = i
            })
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            data = embeddings,
            model = "text-embedding-3-small"
        });
    }

    #endregion
}
