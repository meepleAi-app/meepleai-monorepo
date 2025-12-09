using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>
/// ISSUE-1725: Tests for streaming token tracking in OpenRouterLlmClient
/// </summary>
public class OpenRouterLlmClientStreamingTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ILlmCostCalculator> _costCalculatorMock;
    private readonly Mock<ILogger<OpenRouterLlmClient>> _loggerMock;
    private readonly IConfiguration _config;
    private readonly OpenRouterLlmClient _sut;

    public OpenRouterLlmClientStreamingTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _costCalculatorMock = new Mock<ILlmCostCalculator>();
        _loggerMock = new Mock<ILogger<OpenRouterLlmClient>>();

        var configData = new Dictionary<string, string?>
        {
            { "OPENROUTER_API_KEY", "test-key-12345" }
        };
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(httpClient);

        _sut = new OpenRouterLlmClient(
            _httpClientFactoryMock.Object,
            _config,
            _costCalculatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateCompletionStreamAsync_WithUsageMetadata_ReturnsStreamChunksWithFinalUsage()
    {
        // Arrange
        var model = "openai/gpt-4o-mini";
        var systemPrompt = "You are a helpful assistant";
        var userPrompt = "Hello";

        // Mock SSE response with content chunks + final usage chunk
        var sseResponse = """
            data: {"id":"gen-1","choices":[{"delta":{"content":"Hello"}}],"model":"openai/gpt-4o-mini"}

            data: {"id":"gen-1","choices":[{"delta":{"content":" there"}}],"model":"openai/gpt-4o-mini"}

            data: {"id":"gen-1","choices":[{"delta":{"content":"!"}}],"model":"openai/gpt-4o-mini","usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}

            data: [DONE]
            """;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(sseResponse, Encoding.UTF8, "text/event-stream")
            });

        _costCalculatorMock
            .Setup(c => c.CalculateCost(model, "OpenRouter", 10, 5))
            .Returns(new LlmCostCalculation
            {
                ModelId = model,
                Provider = "OpenRouter",
                PromptTokens = 10,
                CompletionTokens = 5,
                InputCost = 0.00015m,
                OutputCost = 0.0006m
            });

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _sut.GenerateCompletionStreamAsync(model, systemPrompt, userPrompt, 0.7, 100))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(4, chunks.Count);

        // First 3 chunks: content only
        Assert.Equal("Hello", chunks[0].Content);
        Assert.Null(chunks[0].Usage);
        Assert.False(chunks[0].IsFinal);

        Assert.Equal(" there", chunks[1].Content);
        Assert.Null(chunks[1].Usage);

        Assert.Equal("!", chunks[2].Content);
        Assert.Null(chunks[2].Usage);

        // Final chunk: usage metadata
        var finalChunk = chunks[3];
        Assert.Null(finalChunk.Content);
        Assert.True(finalChunk.IsFinal);
        Assert.NotNull(finalChunk.Usage);
        Assert.NotNull(finalChunk.Cost);

        // Verify usage
        Assert.Equal(10, finalChunk.Usage!.PromptTokens);
        Assert.Equal(5, finalChunk.Usage.CompletionTokens);
        Assert.Equal(15, finalChunk.Usage.TotalTokens);

        // Verify cost
        Assert.Equal(0.00015m, finalChunk.Cost!.InputCost);
        Assert.Equal(0.0006m, finalChunk.Cost.OutputCost);
        Assert.Equal(model, finalChunk.Cost.ModelId);
        Assert.Equal("OpenRouter", finalChunk.Cost.Provider);
    }

    [Fact]
    public async Task GenerateCompletionStreamAsync_WithoutUsageMetadata_ReturnsContentChunksOnly()
    {
        // Arrange
        var model = "openai/gpt-4o-mini";

        // Mock SSE response without usage (older API or usage.include=false)
        var sseResponse = """
            data: {"id":"gen-1","choices":[{"delta":{"content":"Test"}}],"model":"openai/gpt-4o-mini"}

            data: [DONE]
            """;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(sseResponse, Encoding.UTF8, "text/event-stream")
            });

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _sut.GenerateCompletionStreamAsync(model, "sys", "usr", 0.7, 100))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.Equal("Test", chunks[0].Content);
        Assert.Null(chunks[0].Usage);
        Assert.False(chunks[0].IsFinal);
    }
}
