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
/// Comprehensive unit tests for OpenRouterLlmClient
/// ISSUE-961: BGAI-019 - Complete LLM client testing
/// </summary>
public class OpenRouterLlmClientTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void SupportsModel_OpenRouterModel_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        client.SupportsModel("openai/gpt-4o-mini").Should().BeTrue();
        client.SupportsModel("anthropic/claude-3.5-haiku").Should().BeTrue();
        client.SupportsModel("meta-llama/llama-3.3-70b-instruct:free").Should().BeTrue();
    }

    [Fact]
    public void SupportsModel_LocalModel_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        client.SupportsModel("llama3:8b").Should().BeFalse();
        client.SupportsModel("mistral").Should().BeFalse();
    }

    [Fact]
    public void ProviderName_ReturnsOpenRouter()
    {
        // Arrange & Act
        var client = CreateClient();

        // Assert
        client.ProviderName.Should().Be("OpenRouter");
    }

    [Fact]
    public async Task GenerateCompletion_Success_ReturnsResult()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var responseJson = JsonSerializer.Serialize(new
        {
            id = "gen-123",
            model = "openai/gpt-4o-mini",
            choices = new[]
            {
                new
                {
                    message = new { role = "assistant", content = "Test response" },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
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
            "openai/gpt-4o-mini",
            "You are helpful",
            "Hello",
            0.7,
            100,
            TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Response.Should().Be("Test response");
        result.Usage.TotalTokens.Should().Be(15);
        result.Usage.PromptTokens.Should().Be(10);
        result.Usage.CompletionTokens.Should().Be(5);
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task GenerateCompletion_ApiError_ReturnsFailure()
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
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("{\"error\": \"Invalid API key\"}")
            });

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "openai/gpt-4o-mini", "system", "prompt", 0.7, 100, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("401");
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
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "openai/gpt-4o-mini", "system", "prompt", 0.7, 100, TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().ContainEquivalentOf("timed out");
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task GenerateCompletionStream_Success_YieldsChunks()
    {
        // Arrange - SSE format streaming
        var mockHandler = new Mock<HttpMessageHandler>();
        var streamContent = string.Join("\n", new[]
        {
            "data: " + JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content = "Hello " } } } }),
            "data: " + JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content = "world" } } } }),
            "data: [DONE]"
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
            "openai/gpt-4o-mini", "system", "prompt", 0.7, 100, TestCancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert - verify content chunks (usage chunk may follow)
        var contentChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        contentChunks.Count.Should().Be(2);
        contentChunks[0].Content.Should().Be("Hello ");
        contentChunks[1].Content.Should().Be("world");
    }

    [Fact]
    public async Task ItalianQuery_ReturnsItalianResponse()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var italianResponse = "Il cavallo si muove a forma di L: due caselle in una direzione e una perpendicolare.";
        var responseJson = JsonSerializer.Serialize(new
        {
            id = "gen-456",
            model = "openai/gpt-4o-mini",
            choices = new[]
            {
                new
                {
                    message = new { role = "assistant", content = italianResponse },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 20, completion_tokens = 15, total_tokens = 35 }
        });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Content != null &&
                    req.Content.ReadAsStringAsync().Result.Contains("Come si muove")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var client = CreateClient(mockHandler.Object);

        // Act
        var result = await client.GenerateCompletionAsync(
            "openai/gpt-4o-mini",
            "Sei un esperto di scacchi",
            "Come si muove il cavallo?",
            0.7,
            100,
            TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Response.Should().Contain("forma di L");
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task ConcurrentRequests_HandledCorrectly()
    {
        // Arrange
        var responseJson = JsonSerializer.Serialize(new
        {
            id = "gen-concurrent",
            choices = new[] { new { message = new { content = "Response" }, finish_reason = "stop" } },
            usage = new { prompt_tokens = 5, completion_tokens = 3, total_tokens = 8 }
        });

        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler);

        // Act - 5 concurrent
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => client.GenerateCompletionAsync(
                "openai/gpt-4o-mini", "sys", "prompt", 0.7, 100, TestCancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Length.Should().Be(5);
        results.Should().OnlyContain(r => r.Success);
    }

    private static OpenRouterLlmClient CreateClient(HttpMessageHandler? handler = null)
    {
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var handlerInstance = handler ?? new Mock<HttpMessageHandler>().Object;
        var httpClient = new HttpClient(handlerInstance)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key-12345"
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
                    InputCost = 0.000015m,  // Mock cost
                    OutputCost = 0.000060m
                });

        var logger = Mock.Of<ILogger<OpenRouterLlmClient>>();

        return new OpenRouterLlmClient(mockHttpClientFactory.Object, config, mockCostCalculator.Object, logger);
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

