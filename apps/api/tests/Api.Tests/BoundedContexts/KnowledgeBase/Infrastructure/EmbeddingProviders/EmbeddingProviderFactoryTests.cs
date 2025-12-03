using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;

/// <summary>
/// Unit tests for EmbeddingProviderFactory.
/// ADR-016 Phase 2: Multi-provider embedding abstraction.
/// Tests factory creation, primary/fallback provider selection, and error handling.
/// </summary>
public class EmbeddingProviderFactoryTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly HttpClient _defaultHttpClient;
    private bool _disposed;

    public EmbeddingProviderFactoryTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _defaultHttpClient = new HttpClient();

        // Setup default HTTP client
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(_defaultHttpClient);

        // Setup logger factory to return null loggers (safe for testing)
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
    }

    /// <summary>
    /// FIX: Dispose HttpClient to prevent socket exhaustion in test suites.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _defaultHttpClient.Dispose();
            _disposed = true;
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var config = CreateDefaultConfiguration();

        // Act
        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var config = CreateDefaultConfiguration();

        // Act
        Action act = () => new EmbeddingProviderFactory(
            null!,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var config = CreateDefaultConfiguration();

        // Act
        Action act = () => new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            null!,
            Options.Create(config));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerFactory");
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    #endregion

    #region GetPrimaryProvider Tests

    [Theory]
    [InlineData(EmbeddingProviderType.OpenRouterLarge)]
    [InlineData(EmbeddingProviderType.OpenRouterSmall)]
    public void GetPrimaryProvider_WithOpenRouterConfig_ReturnsOpenRouterProvider(EmbeddingProviderType providerType)
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = providerType;
        config.OpenRouterApiKey = "test-api-key";

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var provider = factory.GetPrimaryProvider();

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderName.Should().Be("OpenRouter");
    }

    [Theory]
    [InlineData(EmbeddingProviderType.OllamaNomic)]
    [InlineData(EmbeddingProviderType.OllamaMxbai)]
    public void GetPrimaryProvider_WithOllamaConfig_ReturnsOllamaProvider(EmbeddingProviderType providerType)
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = providerType;
        config.OllamaUrl = "http://localhost:11434";

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var provider = factory.GetPrimaryProvider();

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderName.Should().Be("Ollama");
    }

    [Fact]
    public void GetPrimaryProvider_WithHuggingFaceConfig_ReturnsHuggingFaceProvider()
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = EmbeddingProviderType.HuggingFaceBgeM3;
        config.HuggingFaceApiKey = "test-api-key";

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var provider = factory.GetPrimaryProvider();

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderName.Should().Be("HuggingFace");
    }

    [Fact]
    public void GetPrimaryProvider_CalledMultipleTimes_ReturnsProviderWithSameConfiguration()
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = EmbeddingProviderType.OllamaNomic;

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var provider1 = factory.GetPrimaryProvider();
        var provider2 = factory.GetPrimaryProvider();

        // Assert - Factory creates new instances but with same configuration
        provider1.ProviderName.Should().Be(provider2.ProviderName);
        provider1.ModelName.Should().Be(provider2.ModelName);
        provider1.Dimensions.Should().Be(provider2.Dimensions);
    }

    #endregion

    #region GetFallbackProvider Tests

    [Fact]
    public void GetFallbackProvider_WithFallbackConfigured_ReturnsFallbackProvider()
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = EmbeddingProviderType.OllamaNomic;
        config.FallbackProvider = EmbeddingProviderType.OpenRouterSmall;
        config.OpenRouterApiKey = "test-api-key";

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var fallbackProvider = factory.GetFallbackProvider();

        // Assert
        fallbackProvider.Should().NotBeNull();
        fallbackProvider!.ProviderName.Should().Be("OpenRouter");
    }

    [Fact]
    public void GetFallbackProvider_WithNoFallbackConfigured_ReturnsNull()
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = EmbeddingProviderType.OllamaNomic;
        config.FallbackProvider = null;

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var fallbackProvider = factory.GetFallbackProvider();

        // Assert
        fallbackProvider.Should().BeNull();
    }

    [Fact]
    public void GetFallbackProvider_WithFallbackDisabled_ReturnsNull()
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = EmbeddingProviderType.OllamaNomic;
        config.FallbackProvider = EmbeddingProviderType.OpenRouterSmall;
        config.EnableFallback = false;

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var fallbackProvider = factory.GetFallbackProvider();

        // Assert
        fallbackProvider.Should().BeNull();
    }

    #endregion

    #region Provider Properties Tests

    [Theory]
    [InlineData(EmbeddingProviderType.OpenRouterLarge, 3072)]
    [InlineData(EmbeddingProviderType.OpenRouterSmall, 1536)]
    [InlineData(EmbeddingProviderType.OllamaNomic, 768)]
    [InlineData(EmbeddingProviderType.OllamaMxbai, 1024)]
    [InlineData(EmbeddingProviderType.HuggingFaceBgeM3, 1024)]
    public void GetPrimaryProvider_ReturnsCorrectDimensions(
        EmbeddingProviderType providerType,
        int expectedDimensions)
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = providerType;
        config.OpenRouterApiKey = "test-key";
        config.HuggingFaceApiKey = "test-key";

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var provider = factory.GetPrimaryProvider();

        // Assert
        provider.Dimensions.Should().Be(expectedDimensions);
    }

    [Theory]
    [InlineData(EmbeddingProviderType.OpenRouterLarge, "text-embedding-3-large")]
    [InlineData(EmbeddingProviderType.OpenRouterSmall, "text-embedding-3-small")]
    [InlineData(EmbeddingProviderType.OllamaNomic, "nomic-embed-text")]
    [InlineData(EmbeddingProviderType.OllamaMxbai, "mxbai-embed-large")]
    [InlineData(EmbeddingProviderType.HuggingFaceBgeM3, "BAAI/bge-m3")]
    public void GetPrimaryProvider_ReturnsCorrectModelName(
        EmbeddingProviderType providerType,
        string expectedModelName)
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = providerType;
        config.OpenRouterApiKey = "test-key";
        config.HuggingFaceApiKey = "test-key";

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var provider = factory.GetPrimaryProvider();

        // Assert
        provider.ModelName.Should().Be(expectedModelName);
    }

    #endregion

    #region Configuration Override Tests

    [Fact]
    public void GetPrimaryProvider_WithCustomDimensions_UsesConfiguredDimensions()
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = EmbeddingProviderType.OpenRouterLarge;
        config.OpenRouterApiKey = "test-key";
        config.Dimensions = 1024; // Override default 3072

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var provider = factory.GetPrimaryProvider();

        // Assert
        provider.Dimensions.Should().Be(1024);
    }

    [Fact]
    public void GetPrimaryProvider_WithCustomModel_UsesConfiguredModel()
    {
        // Arrange
        var config = CreateDefaultConfiguration();
        config.Provider = EmbeddingProviderType.OpenRouterLarge;
        config.OpenRouterApiKey = "test-key";
        config.Model = "custom-model-name";

        var factory = new EmbeddingProviderFactory(
            _httpClientFactoryMock.Object,
            _loggerFactoryMock.Object,
            Options.Create(config));

        // Act
        var provider = factory.GetPrimaryProvider();

        // Assert
        provider.ModelName.Should().Be("custom-model-name");
    }

    #endregion

    #region Helper Methods

    private static EmbeddingConfiguration CreateDefaultConfiguration()
    {
        return new EmbeddingConfiguration
        {
            Provider = EmbeddingProviderType.OllamaNomic,
            BatchSize = 10,
            MaxRetries = 3,
            TimeoutSeconds = 60,
            EnableFallback = true,
            OllamaUrl = "http://localhost:11434"
        };
    }

    #endregion
}
