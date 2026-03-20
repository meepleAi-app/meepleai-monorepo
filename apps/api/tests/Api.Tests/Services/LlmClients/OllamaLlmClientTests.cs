using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services.LlmClients;

/// <summary>
/// Comprehensive unit tests for OllamaLlmClient
/// ISSUE-961: BGAI-019 - Complete LLM client testing (12 scenarios)
/// </summary>
public class OllamaLlmClientTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void SupportsModel_LocalModel_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        client.SupportsModel("llama3:8b").Should().BeTrue();
        client.SupportsModel("mistral:latest").Should().BeTrue();
        client.SupportsModel("qwen:7b").Should().BeTrue();
    }

    [Fact]
    public void SupportsModel_OpenRouterModel_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        client.SupportsModel("openai/gpt-4o-mini").Should().BeFalse();
        client.SupportsModel("anthropic/claude-3.5-haiku").Should().BeFalse();
    }

    [Fact]
    public void ProviderName_ReturnsOllama()
    {
        // Arrange & Act
        var client = CreateClient();

        // Assert
        client.ProviderName.Should().Be("Ollama");
    }

    [Fact]
    public async Task GenerateCompletion_Success_ReturnsResult()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var responseJson = JsonSerializer.Serialize(new
        {
            model = "llama3:8b",
            message = new { role = "assistant", content = "Test response" },
            done = true,
            done_reason = "stop"
        });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage _, CancellationToken _) => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "llama3:8b",
            "You are a helpful assistant",
            "Hello",
            0.7,
            100,
            TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Response.Should().Be("Test response");
        result.Usage.Should().NotBeNull();
        (result.Usage.TotalTokens > 0).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateCompletion_Timeout_ReturnsFailure()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "llama3:8b",
            "system",
            "user prompt",
            0.7,
            100,
            TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().ContainEquivalentOf("timed out");
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task GenerateCompletion_ModelNotFound_ReturnsError()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("{\"error\": \"model not found\"}")
            });

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "nonexistent:model",
            "system",
            "prompt",
            0.7,
            100,
            TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("404");
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task GenerateCompletionStream_Success_YieldsChunks()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var streamContent = string.Join("\n", new[]
        {
            JsonSerializer.Serialize(new { message = new { content = "Hello " }, done = false }),
            JsonSerializer.Serialize(new { message = new { content = "world" }, done = false }),
            JsonSerializer.Serialize(new { message = new { content = "!" }, done = true })
        });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(streamContent)
            });

        var client = CreateClient(mockHandler.Object);

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.GenerateCompletionStreamAsync(
            "llama3:8b", "system", "prompt", 0.7, 100, TestCancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert - verify content chunks (usage chunk may follow)
        var contentChunks = chunks.Where(c => c.Content != null).ToList();
        contentChunks.Count.Should().Be(3);
        contentChunks[0].Content.Should().Be("Hello ");
        contentChunks[1].Content.Should().Be("world");
        contentChunks[2].Content.Should().Be("!");
    }

    [Fact]
    public async Task ItalianQuery_ReturnsItalianResponse()
    {
        // Arrange - Test Italian language handling
        var mockHandler = new Mock<HttpMessageHandler>();
        var italianResponse = "Il cavallo si muove a forma di L";
        var responseJson = JsonSerializer.Serialize(new
        {
            model = "llama3:8b",
            message = new { role = "assistant", content = italianResponse },
            done = true
        });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Content != null &&
                    req.Content.ReadAsStringAsync().Result.Contains("Come si muove il cavallo")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "llama3:8b",
            "Sei un assistente per regole di giochi da tavolo",
            "Come si muove il cavallo negli scacchi?",
            0.7,
            100,
            TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Response.Should().Be(italianResponse);
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task ConcurrentRequests_HandledCorrectly()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            model = "llama3:8b",
            message = new { role = "assistant", content = "Response" },
            done = true
        });

        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);

        // Act - 5 concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => client.GenerateCompletionAsync(
                "llama3:8b", "system", "prompt", 0.7, 100, TestCancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Length.Should().Be(5);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task ResponseParsing_InvalidJson_ReturnsFailure()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Invalid JSON {{{", Encoding.UTF8, "application/json")
            });

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "llama3:8b", "system", "prompt", 0.7, 100, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid response format");
    }

    [Fact]
    public async Task EmptyPrompt_ReturnsFailure()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var result = await client.GenerateCompletionAsync(
            "llama3:8b", "system", "", 0.7, 100, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No user prompt");
    }

    [Fact]
    public async Task HttpRequestException_ReturnsFailure()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "llama3:8b", "system", "prompt", 0.7, 100, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP error");
    }

    private static OllamaLlmClient CreateClient(HttpMessageHandler? handler = null)
    {
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockHandler = handler ?? new Mock<HttpMessageHandler>().Object;
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["OllamaUrl"] = "http://localhost:11434"
            }!)
            .Build();

        var mockCostCalculator = new Mock<ILlmCostCalculator>();
        mockCostCalculator.Setup(c => c.CalculateCost(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<int>()))
            .Returns((string modelId, string provider, int prompt, int completion) =>
                new Api.BoundedContexts.KnowledgeBase.Domain.Models.LlmCostCalculation
                {
                    ModelId = modelId,
                    Provider = provider,
                    PromptTokens = prompt,
                    CompletionTokens = completion,
                    InputCost = 0m,  // Ollama is free (self-hosted)
                    OutputCost = 0m
                });

        var logger = Mock.Of<ILogger<OllamaLlmClient>>();

        return new OllamaLlmClient(mockHttpClientFactory.Object, config, mockCostCalculator.Object, logger);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request, cancellationToken));
        }
    }
}

