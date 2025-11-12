using Api.Services.LlmClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>
/// Unit tests for OllamaLlmClient
/// ISSUE-958: Validates Ollama provider integration
/// </summary>
public class OllamaLlmClientTests
{
    [Fact]
    public void Test01_SupportsModel_LocalModel_ReturnsTrue()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        Assert.True(client.SupportsModel("llama3:8b"));
        Assert.True(client.SupportsModel("mistral:latest"));
    }

    [Fact]
    public void Test02_SupportsModel_OpenRouterModel_ReturnsFalse()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        Assert.False(client.SupportsModel("openai/gpt-4o-mini"));
        Assert.False(client.SupportsModel("anthropic/claude-3.5-haiku"));
    }

    [Fact]
    public void Test03_ProviderName_ReturnsOllama()
    {
        // Arrange & Act
        var client = CreateClient();

        // Assert
        Assert.Equal("Ollama", client.ProviderName);
    }

    private static OllamaLlmClient CreateClient()
    {
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object)
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

        var logger = Mock.Of<ILogger<OllamaLlmClient>>();

        return new OllamaLlmClient(mockHttpClientFactory.Object, config, logger);
    }
}
