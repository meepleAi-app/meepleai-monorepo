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
/// ISSUE-1725: Tests for streaming token tracking in OllamaLlmClient
/// </summary>
public class OllamaLlmClientStreamingTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ILlmCostCalculator> _costCalculatorMock;
    private readonly Mock<ILogger<OllamaLlmClient>> _loggerMock;
    private readonly IConfiguration _config;
    private readonly OllamaLlmClient _sut;

    public OllamaLlmClientStreamingTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _costCalculatorMock = new Mock<ILlmCostCalculator>();
        _loggerMock = new Mock<ILogger<OllamaLlmClient>>();

        var configData = new Dictionary<string, string?>
        {
            { "OLLAMA_URL", "http://localhost:11434" }
        };
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient("Ollama")).Returns(httpClient);

        _sut = new OllamaLlmClient(
            _httpClientFactoryMock.Object,
            _config,
            _costCalculatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateCompletionStreamAsync_WithUsageMetadata_ReturnsStreamChunksWithFinalUsage()
    {
        // Arrange
        var model = "llama3.3:70b";

        // Mock Ollama streaming response (JSON line-by-line format)
        var streamResponse = """
            {"model":"llama3.3:70b","message":{"role":"assistant","content":"Hello"},"done":false}
            {"model":"llama3.3:70b","message":{"role":"assistant","content":" there"},"done":false}
            {"model":"llama3.3:70b","message":{"role":"assistant","content":"!"},"done":false}
            {"model":"llama3.3:70b","message":{"role":"assistant","content":""},"done":true,"prompt_eval_count":25,"eval_count":10}
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
                Content = new StringContent(streamResponse, Encoding.UTF8, "application/json")
            });

        _costCalculatorMock
            .Setup(c => c.CalculateCost(model, "Ollama", 25, 10))
            .Returns(new LlmCostCalculation
            {
                ModelId = model,
                Provider = "Ollama",
                PromptTokens = 25,
                CompletionTokens = 10,
                InputCost = 0.0m, // Ollama is free
                OutputCost = 0.0m
            });

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _sut.GenerateCompletionStreamAsync(model, "sys", "user", 0.7, 100))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(4, chunks.Count);

        // First 3 chunks: content
        Assert.Equal("Hello", chunks[0].Content);
        Assert.Null(chunks[0].Usage);
        Assert.False(chunks[0].IsFinal);

        Assert.Equal(" there", chunks[1].Content);
        Assert.Equal("!", chunks[2].Content);

        // Final chunk: usage metadata
        var finalChunk = chunks[3];
        Assert.Null(finalChunk.Content);
        Assert.True(finalChunk.IsFinal);
        Assert.NotNull(finalChunk.Usage);
        Assert.NotNull(finalChunk.Cost);

        // Verify usage (Ollama provides prompt_eval_count and eval_count)
        Assert.Equal(25, finalChunk.Usage!.PromptTokens);
        Assert.Equal(10, finalChunk.Usage.CompletionTokens);
        Assert.Equal(35, finalChunk.Usage.TotalTokens);

        // Verify cost (Ollama is free - zero cost)
        Assert.Equal(0.0m, finalChunk.Cost!.TotalCost);
        Assert.Equal("Ollama", finalChunk.Cost.Provider);
    }

    [Fact]
    public async Task GenerateCompletionStreamAsync_WithoutUsageInFinalChunk_LogsWarning()
    {
        // Arrange
        var model = "llama3.3:70b";

        // Mock response with done=true but no usage fields
        var streamResponse = """
            {"model":"llama3.3:70b","message":{"role":"assistant","content":"Test"},"done":false}
            {"model":"llama3.3:70b","message":{"role":"assistant","content":""},"done":true}
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
                Content = new StringContent(streamResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in _sut.GenerateCompletionStreamAsync(model, "sys", "usr", 0.7, 100))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks); // Only content chunk, no final usage chunk
        Assert.Equal("Test", chunks[0].Content);
        Assert.Null(chunks[0].Usage);

        // Verify warning logged
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("without usage metadata")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
