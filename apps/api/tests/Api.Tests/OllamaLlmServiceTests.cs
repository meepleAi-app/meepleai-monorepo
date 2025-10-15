using System.Net;
using System.Text;
using System.Text.Json;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Comprehensive tests for OllamaLlmService covering:
/// - Successful chat completions
/// - Error handling (HTTP errors, timeouts, null responses)
/// - Token estimation
/// - Request/response serialization
/// - Configuration handling (OLLAMA_URL, model selection)
/// - Cancellation token support
/// </summary>
public class OllamaLlmServiceTests
{
    private readonly Mock<ILogger<OllamaLlmService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public OllamaLlmServiceTests()
    {
        _mockLogger = new Mock<ILogger<OllamaLlmService>>();
        _mockConfig = new Mock<IConfiguration>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();

        // Default configuration
        _mockConfig.Setup(c => c["OLLAMA_URL"]).Returns("http://ollama:11434");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var service = CreateService();
        var systemPrompt = "You are a helpful assistant.";
        var userPrompt = "What is 2+2?";

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "The answer is 4." },
            done = true,
            done_reason = "stop",
            total_duration = 150_000_000L, // 150ms in nanoseconds
            prompt_eval_count = 10,
            eval_count = 5
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(ollamaResponse));

        // Act
        var result = await service.GenerateCompletionAsync(systemPrompt, userPrompt);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("The answer is 4.", result.Response);
        Assert.NotNull(result.Usage);
        Assert.Equal(10, result.Usage.PromptTokens);
        Assert.Equal(5, result.Usage.CompletionTokens);
        Assert.Equal(15, result.Usage.TotalTokens);
        Assert.NotNull(result.Metadata);
        Assert.Equal("llama3.2:3b", result.Metadata["model"]);
        Assert.Equal("stop", result.Metadata["done_reason"]);
        Assert.Equal("150", result.Metadata["total_duration_ms"]);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithEmptyUserPrompt_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateCompletionAsync("System prompt", "");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No user prompt provided", result.ErrorMessage);
        Assert.Empty(result.Response);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithWhitespaceUserPrompt_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateCompletionAsync("System prompt", "   ");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No user prompt provided", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithNullUserPrompt_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateCompletionAsync("System prompt", null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No user prompt provided", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithEmptySystemPrompt_SendsOnlyUserMessage()
    {
        // Arrange
        var service = CreateService();
        var userPrompt = "What is 2+2?";

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "4" },
            done = true,
            prompt_eval_count = 5,
            eval_count = 1
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(ollamaResponse));

        // Act
        var result = await service.GenerateCompletionAsync("", userPrompt);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("4", result.Response);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenHttpError_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal server error");

        // Act
        var result = await service.GenerateCompletionAsync("System", "User prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API error: InternalServerError", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenHttpBadRequest_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        SetupHttpResponse(HttpStatusCode.BadRequest, "Invalid request");

        // Act
        var result = await service.GenerateCompletionAsync("System", "User prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API error: BadRequest", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenNullMessageInResponse_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = (object?)null, // Null message
            done = true
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(ollamaResponse));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No response returned from Ollama", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenEmptyContentInResponse_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "" }, // Empty content
            done = true
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(ollamaResponse));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No response returned from Ollama", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenTimeoutException_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Request timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenGeneralException_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Error: Network error", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithMissingTokenCounts_EstimatesTokens()
    {
        // Arrange
        var service = CreateService();
        var systemPrompt = "You are a helpful assistant.";
        var userPrompt = "What is 2+2?";

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "The answer is 4." },
            done = true,
            prompt_eval_count = 0, // No token counts provided
            eval_count = 0
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(ollamaResponse));

        // Act
        var result = await service.GenerateCompletionAsync(systemPrompt, userPrompt);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Usage);
        // Estimated tokens should be > 0 (roughly length/4)
        Assert.True(result.Usage.PromptTokens > 0);
        Assert.True(result.Usage.CompletionTokens > 0);
        Assert.Equal(result.Usage.PromptTokens + result.Usage.CompletionTokens, result.Usage.TotalTokens);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithCustomOllamaUrl_UsesConfiguredUrl()
    {
        // Arrange
        _mockConfig.Setup(c => c["OLLAMA_URL"]).Returns("http://custom-ollama:9999");
        var service = CreateService();

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "Response" },
            done = true
        };

        HttpRequestMessage? capturedRequest = null;
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(ollamaResponse), Encoding.UTF8, "application/json")
            });

        // Act
        await service.GenerateCompletionAsync("System", "User");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("http://custom-ollama:9999/api/chat", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithNullOllamaUrl_UsesDefaultUrl()
    {
        // Arrange
        _mockConfig.Setup(c => c["OLLAMA_URL"]).Returns((string?)null);
        var service = CreateService();

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "Response" },
            done = true
        };

        HttpRequestMessage? capturedRequest = null;
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(ollamaResponse), Encoding.UTF8, "application/json")
            });

        // Act
        await service.GenerateCompletionAsync("System", "User");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("http://ollama:11434/api/chat", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task GenerateCompletionAsync_SendsCorrectRequestStructure()
    {
        // Arrange
        var service = CreateService();
        var systemPrompt = "You are a board game assistant.";
        var userPrompt = "Explain chess rules.";

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "Chess rules..." },
            done = true
        };

        string? capturedRequestBody = null;
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedRequestBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(ollamaResponse), Encoding.UTF8, "application/json")
            });

        // Act
        await service.GenerateCompletionAsync(systemPrompt, userPrompt);

        // Assert
        Assert.NotNull(capturedRequestBody);

        using var doc = JsonDocument.Parse(capturedRequestBody);
        var root = doc.RootElement;

        Assert.Equal("llama3.2:3b", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());

        var systemMessage = messages[0];
        Assert.Equal("system", systemMessage.GetProperty("role").GetString());
        Assert.Equal(systemPrompt, systemMessage.GetProperty("content").GetString());

        var userMessage = messages[1];
        Assert.Equal("user", userMessage.GetProperty("role").GetString());
        Assert.Equal(userPrompt, userMessage.GetProperty("content").GetString());

        var options = root.GetProperty("options");
        Assert.Equal(0.3f, options.GetProperty("temperature").GetSingle());
        Assert.Equal(500, options.GetProperty("num_predict").GetInt32());
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithCancellationToken_PropagatesCancellation()
    {
        // Arrange
        var service = CreateService();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request cancelled"));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User", cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Request timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithLongResponse_HandlesCorrectly()
    {
        // Arrange
        var service = CreateService();
        var longResponse = new string('a', 5000); // 5000 character response

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = longResponse },
            done = true,
            eval_count = 1250 // Approx 5000/4 tokens
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(ollamaResponse));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(longResponse, result.Response);
        Assert.Equal(1250, result.Usage?.CompletionTokens);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithSpecialCharactersInPrompt_HandlesCorrectly()
    {
        // Arrange
        var service = CreateService();
        var userPrompt = "What does \"e4 e5\" mean in chess? Include symbols: ♔♕♖♗♘♙";

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "It's an opening move." },
            done = true
        };

        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(ollamaResponse));

        // Act
        var result = await service.GenerateCompletionAsync("System", userPrompt);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Response);
    }

    /// <summary>
    /// Creates OllamaLlmService with mocked dependencies
    /// </summary>
    private OllamaLlmService CreateService()
    {
        var httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("http://ollama:11434")
        };

        _mockHttpClientFactory
            .Setup(f => f.CreateClient("Ollama"))
            .Returns(httpClient);

        return new OllamaLlmService(_mockHttpClientFactory.Object, _mockConfig.Object, _mockLogger.Object);
    }

    /// <summary>
    /// Configures mock HTTP handler to return specified response
    /// </summary>
    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}
