using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services.LlmClients;

/// <summary>
/// Unit tests for DeepSeekLlmClient
/// ISSUE-419: Direct DeepSeek provider — unconfigured state and model routing
/// </summary>
public class DeepSeekLlmClientTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // ─── ProviderName ────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void ProviderName_ReturnsDeepSeek()
    {
        // Arrange & Act
        var client = CreateUnconfiguredClient();

        // Assert
        client.ProviderName.Should().Be("DeepSeek");
    }

    // ─── SupportsModel ───────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void SupportsModel_DeepSeekModels_ReturnsTrue()
    {
        // Arrange
        var client = CreateUnconfiguredClient();

        // Act & Assert
        client.SupportsModel("deepseek-chat").Should().BeTrue();
        client.SupportsModel("deepseek-coder").Should().BeTrue();
        client.SupportsModel("deepseek-reasoner").Should().BeTrue();
        client.SupportsModel("DeepSeek-chat").Should().BeTrue(); // case-insensitive
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void SupportsModel_NonDeepSeekModels_ReturnsFalse()
    {
        // Arrange
        var client = CreateUnconfiguredClient();

        // Act & Assert
        client.SupportsModel("gpt-4o").Should().BeFalse();
        client.SupportsModel("meta-llama/llama-3.3-70b-instruct").Should().BeFalse();
        client.SupportsModel("ollama:llama3").Should().BeFalse();
        client.SupportsModel("openai/gpt-4o-mini").Should().BeFalse();
    }

    // ─── GenerateCompletionAsync — unconfigured ──────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GenerateCompletionAsync_WhenNotConfigured_ReturnsFailure()
    {
        // Arrange
        var client = CreateUnconfiguredClient();

        // Act
        var result = await client.GenerateCompletionAsync(
            "deepseek-chat",
            "You are helpful",
            "Hello",
            0.7,
            100,
            TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    // ─── GenerateCompletionStreamAsync — unconfigured ────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task GenerateCompletionStreamAsync_WhenNotConfigured_YieldsNothing()
    {
        // Arrange
        var client = CreateUnconfiguredClient();
        var chunks = new List<StreamChunk>();

        // Act
        await foreach (var chunk in client.GenerateCompletionStreamAsync(
            "deepseek-chat",
            "You are helpful",
            "Hello",
            0.7,
            100,
            TestCancellationToken))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().BeEmpty();
    }

    // ─── CheckHealthAsync — unconfigured ─────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CheckHealthAsync_WhenNotConfigured_ReturnsFalse()
    {
        // Arrange
        var client = CreateUnconfiguredClient();

        // Act
        var healthy = await client.CheckHealthAsync(TestCancellationToken);

        // Assert
        healthy.Should().BeFalse();
    }

    // ─── Factory helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates a DeepSeekLlmClient with no DEEPSEEK_API_KEY so _isConfigured = false.
    /// </summary>
    private static DeepSeekLlmClient CreateUnconfiguredClient()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var httpClient = new HttpClient();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Empty configuration — no DEEPSEEK_API_KEY present
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>()!)
            .Build();

        var mockCostCalculator = new Mock<ILlmCostCalculator>();
        mockCostCalculator
            .Setup(c => c.CalculateCost(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .Returns((string modelId, string provider, int prompt, int completion) =>
                new LlmCostCalculation
                {
                    ModelId = modelId,
                    Provider = provider,
                    PromptTokens = prompt,
                    CompletionTokens = completion,
                    InputCost = 0.000001m,
                    OutputCost = 0.000002m
                });

        var logger = Mock.Of<ILogger<DeepSeekLlmClient>>();

        return new DeepSeekLlmClient(mockFactory.Object, config, mockCostCalculator.Object, logger);
    }
}
