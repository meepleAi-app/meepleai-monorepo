using System.Net;
using System.Text;
using System.Text.Json;
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
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<OllamaLlmService>> _mockLogger;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public OllamaLlmServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<OllamaLlmService>>();
        _configMock = new Mock<IConfiguration>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        // Default configuration
        _configMock.Setup(c => c["OLLAMA_URL"]).Returns("http://ollama:11434");
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
        result.Success.Should().BeTrue();
        result.Response.Should().Be("The answer is 4.");
        result.Usage.Should().NotBeNull();
        result.Usage.PromptTokens.Should().Be(10);
        result.Usage.CompletionTokens.Should().Be(5);
        result.Usage.TotalTokens.Should().Be(15);
        result.Metadata.Should().NotBeNull();
        result.Metadata["model"].Should().Be("llama3.2:3b");
        result.Metadata["done_reason"].Should().Be("stop");
        result.Metadata["total_duration_ms"].Should().Be("150");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithEmptyUserPrompt_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateCompletionAsync("System prompt", "");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No user prompt provided");
        result.Response.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithWhitespaceUserPrompt_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateCompletionAsync("System prompt", "   ");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No user prompt provided");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithNullUserPrompt_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateCompletionAsync("System prompt", null!);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No user prompt provided");
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
        result.Success.Should().BeTrue();
        result.Response.Should().Be("4");
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API error: InternalServerError");
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API error: BadRequest");
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No response returned from Ollama");
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No response returned from Ollama");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenTimeoutException_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User prompt");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Request timed out");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WhenGeneralException_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User prompt");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Error: Network error");
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
        result.Success.Should().BeTrue();
        result.Usage.Should().NotBeNull();
        // Estimated tokens should be > 0 (roughly length/4)
        (result.Usage.PromptTokens > 0).Should().BeTrue();
        (result.Usage.CompletionTokens > 0).Should().BeTrue();
        result.Usage.TotalTokens.Should().Be(result.Usage.PromptTokens + result.Usage.CompletionTokens);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithCustomOllamaUrl_UsesConfiguredUrl()
    {
        // Arrange
        _configMock.Setup(c => c["OLLAMA_URL"]).Returns("http://custom-ollama:9999");
        var service = CreateService();

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "Response" },
            done = true
        };

        HttpRequestMessage? capturedRequest = null;
        _httpHandlerMock.Protected()
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
        capturedRequest.Should().NotBeNull();
        capturedRequest.RequestUri?.ToString().Should().Be("http://custom-ollama:9999/api/chat");
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithNullOllamaUrl_UsesDefaultUrl()
    {
        // Arrange
        _configMock.Setup(c => c["OLLAMA_URL"]).Returns((string?)null);
        var service = CreateService();

        var ollamaResponse = new
        {
            model = "llama3.2:3b",
            message = new { role = "assistant", content = "Response" },
            done = true
        };

        HttpRequestMessage? capturedRequest = null;
        _httpHandlerMock.Protected()
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
        capturedRequest.Should().NotBeNull();
        capturedRequest.RequestUri?.ToString().Should().Be("http://ollama:11434/api/chat");
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
        _httpHandlerMock.Protected()
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
        capturedRequestBody.Should().NotBeNull();

        using var doc = JsonDocument.Parse(capturedRequestBody);
        var root = doc.RootElement;

        root.GetProperty("model").GetString().Should().Be("llama3.2:3b");
        root.GetProperty("stream").GetBoolean().Should().BeFalse();

        var messages = root.GetProperty("messages");
        messages.GetArrayLength().Should().Be(2);

        var systemMessage = messages[0];
        systemMessage.GetProperty("role").GetString().Should().Be("system");
        systemMessage.GetProperty("content").GetString().Should().Be(systemPrompt);

        var userMessage = messages[1];
        userMessage.GetProperty("role").GetString().Should().Be("user");
        userMessage.GetProperty("content").GetString().Should().Be(userPrompt);

        var options = root.GetProperty("options");
        options.GetProperty("temperature").GetSingle().Should().Be(0.3f);
        options.GetProperty("num_predict").GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task GenerateCompletionAsync_WithCancellationToken_PropagatesCancellation()
    {
        // Arrange
        var service = CreateService();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request cancelled"));

        // Act
        var result = await service.GenerateCompletionAsync("System", "User", cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Request timed out");
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
        result.Success.Should().BeTrue();
        result.Response.Should().Be(longResponse);
        result.Usage?.CompletionTokens.Should().Be(1250);
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
        result.Success.Should().BeTrue();
        result.Response.Should().NotBeNull();
    }

    /// <summary>
    /// Creates OllamaLlmService with mocked dependencies
    /// </summary>
    private OllamaLlmService CreateService()
    {
        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://ollama:11434")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Ollama"))
            .Returns(httpClient);

        return new OllamaLlmService(_httpClientFactoryMock.Object, _configMock.Object, _mockLogger.Object);
    }

    /// <summary>
    /// Configures mock HTTP handler to return specified response
    /// </summary>
    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpHandlerMock.Protected()
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
