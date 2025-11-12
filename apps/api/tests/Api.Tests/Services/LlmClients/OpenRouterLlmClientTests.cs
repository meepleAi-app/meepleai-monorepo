using Api.Services.LlmClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>
/// Unit tests for OpenRouterLlmClient
/// ISSUE-958: Validates OpenRouter provider integration
/// </summary>
public class OpenRouterLlmClientTests
{
    [Fact]
    public void Test01_SupportsModel_OpenRouterModel_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        Assert.True(client.SupportsModel("openai/gpt-4o-mini"));
        Assert.True(client.SupportsModel("anthropic/claude-3.5-haiku"));
        Assert.True(client.SupportsModel("meta-llama/llama-3.3-70b-instruct:free"));
    }

    [Fact]
    public void Test02_SupportsModel_LocalModel_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        Assert.False(client.SupportsModel("llama3:8b"));
        Assert.False(client.SupportsModel("mistral"));
    }

    [Fact]
    public void Test03_ProviderName_ReturnsOpenRouter()
    {
        // Arrange & Act
        var client = CreateClient();

        // Assert
        Assert.Equal("OpenRouter", client.ProviderName);
    }

    private static OpenRouterLlmClient CreateClient()
    {
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var httpClient = new HttpClient
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

        var logger = Mock.Of<ILogger<OpenRouterLlmClient>>();

        return new OpenRouterLlmClient(mockHttpClientFactory.Object, config, logger);
    }
}
