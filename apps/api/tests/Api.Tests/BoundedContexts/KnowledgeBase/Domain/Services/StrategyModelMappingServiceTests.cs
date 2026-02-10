using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class StrategyModelMappingServiceTests
{
    private readonly Mock<IStrategyModelMappingRepository> _mockRepository;
    private readonly Mock<IHybridCacheService> _mockCache;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ILogger<StrategyModelMappingService>> _mockLogger;
    private readonly StrategyModelMappingService _service;

    public StrategyModelMappingServiceTests()
    {
        _mockRepository = new Mock<IStrategyModelMappingRepository>();
        _mockCache = new Mock<IHybridCacheService>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<StrategyModelMappingService>>();

        // Setup service scope factory to return repository
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IStrategyModelMappingRepository)))
            .Returns(_mockRepository.Object);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        _service = new StrategyModelMappingService(
            _mockScopeFactory.Object,
            _mockCache.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetModelMappingAsync_WhenCacheMiss_QueriesRepository()
    {
        // Arrange
        var strategy = RagStrategy.Fast;
        var entry = new StrategyModelMappingEntry(
            "FAST",
            "meta-llama/llama-3.3-70b-instruct:free",
            Array.Empty<string>(),
            "openrouter",
            false,
            false);

        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        // Setup cache to invoke factory function (cache miss scenario)
        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var result = await _service.GetModelMappingAsync(strategy);

        // Assert
        result.Should().NotBeNull();
        result!.Strategy.Should().Be(strategy);
        result.Provider.Should().Be("OpenRouter", "because provider should be normalized");
        result.PrimaryModel.Should().Be("meta-llama/llama-3.3-70b-instruct:free");

        _mockRepository.Verify(
            r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetModelMappingAsync_WhenNoDatabaseEntry_ReturnsDefaultMapping()
    {
        // Arrange
        var strategy = RagStrategy.Precise;

        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StrategyModelMappingEntry?)null);

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var result = await _service.GetModelMappingAsync(strategy);

        // Assert
        result.Should().NotBeNull();
        result!.Strategy.Should().Be(strategy);
        result.Provider.Should().Be("Anthropic");
        result.PrimaryModel.Should().Be("anthropic/claude-sonnet-4.5");
        result.FallbackModels.Should().ContainSingle()
            .Which.Should().Be("openai/gpt-4o-mini");
    }

    [Fact]
    public async Task GetModelMappingAsync_NormalizesProviderName()
    {
        // Arrange
        var strategy = RagStrategy.Fast;
        var entry = new StrategyModelMappingEntry(
            "FAST",
            "some-model",
            Array.Empty<string>(),
            "openrouter", // lowercase in DB
            false,
            false);

        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var result = await _service.GetModelMappingAsync(strategy);

        // Assert
        result!.Provider.Should().Be("OpenRouter", "because provider names should be normalized to PascalCase");
    }

    [Fact]
    public async Task GetModelForStrategyAsync_ReturnsPrimaryModel()
    {
        // Arrange
        var strategy = RagStrategy.Balanced;
        var mapping = StrategyModelMapping.Default(strategy);

        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StrategyModelMappingEntry?)null);

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var (provider, modelId) = await _service.GetModelForStrategyAsync(strategy);

        // Assert
        provider.Should().Be("DeepSeek");
        modelId.Should().Be("deepseek-chat");
    }

    [Fact]
    public async Task GetFallbackModelsAsync_ReturnsConfiguredFallbacks()
    {
        // Arrange
        var strategy = RagStrategy.Precise;

        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StrategyModelMappingEntry?)null);

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var fallbacks = await _service.GetFallbackModelsAsync(strategy);

        // Assert
        fallbacks.Should().ContainSingle()
            .Which.Should().Be("openai/gpt-4o-mini");
    }

    [Fact]
    public async Task GetFallbackModelsAsync_WhenNoFallbacks_ReturnsEmptyList()
    {
        // Arrange
        var strategy = RagStrategy.Fast;

        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StrategyModelMappingEntry?)null);

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var fallbacks = await _service.GetFallbackModelsAsync(strategy);

        // Assert
        fallbacks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetModelMappingAsync_UsesCacheKey()
    {
        // Arrange
        var strategy = RagStrategy.Expert;
        var expectedCacheKey = "strategy-model:EXPERT";

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                expectedCacheKey,
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(StrategyModelMapping.Default(strategy));

        // Act
        await _service.GetModelMappingAsync(strategy);

        // Assert
        _mockCache.Verify(
            c => c.GetOrCreateAsync<StrategyModelMapping>(
                expectedCacheKey,
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetModelMappingAsync_UsesCacheTags()
    {
        // Arrange
        var strategy = RagStrategy.Consensus;
        string[]? capturedTags = null;

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan?, CancellationToken>(
                (k, f, tags, ttl, ct) => capturedTags = tags)
            .ReturnsAsync(StrategyModelMapping.Default(strategy));

        // Act
        await _service.GetModelMappingAsync(strategy);

        // Assert
        capturedTags.Should().NotBeNull();
        capturedTags.Should().Contain("strategy-model-mapping");
        capturedTags.Should().Contain("strategy:CONSENSUS");
    }

    [Fact]
    public async Task GetModelMappingAsync_WithDatabaseEntry_MapsToStrategyModelMapping()
    {
        // Arrange
        var strategy = RagStrategy.Custom;
        var entry = new StrategyModelMappingEntry(
            "CUSTOM",
            "custom-model-id",
            new[] { "fallback-1", "fallback-2" },
            "ollama",
            true,
            true);

        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var result = await _service.GetModelMappingAsync(strategy);

        // Assert
        result.Should().NotBeNull();
        result!.Strategy.Should().Be(strategy);
        result.Provider.Should().Be("Ollama");
        result.PrimaryModel.Should().Be("custom-model-id");
        result.FallbackModels.Should().HaveCount(2)
            .And.Contain("fallback-1")
            .And.Contain("fallback-2");
        result.IsCustomizable.Should().BeTrue();
    }

    [Theory]
    [InlineData(RagStrategy.Fast, "OpenRouter", "meta-llama/llama-3.3-70b-instruct:free")]
    [InlineData(RagStrategy.Balanced, "DeepSeek", "deepseek-chat")]
    [InlineData(RagStrategy.Precise, "Anthropic", "anthropic/claude-sonnet-4.5")]
    [InlineData(RagStrategy.Expert, "Anthropic", "anthropic/claude-sonnet-4.5")]
    [InlineData(RagStrategy.Custom, "Anthropic", "anthropic/claude-haiku-4.5")]
    public async Task GetModelForStrategyAsync_ReturnsCorrectDefaults(
        RagStrategy strategy,
        string expectedProvider,
        string expectedModel)
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StrategyModelMappingEntry?)null);

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var (provider, modelId) = await _service.GetModelForStrategyAsync(strategy);

        // Assert
        provider.Should().Be(expectedProvider);
        modelId.Should().Be(expectedModel);
    }

    [Fact]
    public async Task GetFallbackModelsAsync_Consensus_ReturnsMultipleFallbacks()
    {
        // Arrange
        var strategy = RagStrategy.Consensus;

        _mockRepository
            .Setup(r => r.GetByStrategyAsync(strategy, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StrategyModelMappingEntry?)null);

        _mockCache
            .Setup(c => c.GetOrCreateAsync<StrategyModelMapping>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<StrategyModelMapping>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<StrategyModelMapping>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, ttl, ct) => await factory(ct));

        // Act
        var fallbacks = await _service.GetFallbackModelsAsync(strategy);

        // Assert
        fallbacks.Should().HaveCount(2)
            .And.Contain("openai/gpt-4o")
            .And.Contain("google/gemini-pro");
    }

    [Fact]
    public async Task StrategyModelMapping_Default_UsesConfigurationForAllStrategies()
    {
        // Arrange & Act
        var fastMapping = StrategyModelMapping.Default(RagStrategy.Fast);
        var balancedMapping = StrategyModelMapping.Default(RagStrategy.Balanced);
        var preciseMapping = StrategyModelMapping.Default(RagStrategy.Precise);
        var expertMapping = StrategyModelMapping.Default(RagStrategy.Expert);
        var consensusMapping = StrategyModelMapping.Default(RagStrategy.Consensus);
        var customMapping = StrategyModelMapping.Default(RagStrategy.Custom);

        // Assert
        fastMapping.Provider.Should().Be("OpenRouter");
        balancedMapping.Provider.Should().Be("DeepSeek");
        preciseMapping.Provider.Should().Be("Anthropic");
        expertMapping.Provider.Should().Be("Anthropic");
        consensusMapping.Provider.Should().Be("Mixed");
        customMapping.Provider.Should().Be("Anthropic");

        customMapping.IsCustomizable.Should().BeTrue();
        fastMapping.IsCustomizable.Should().BeFalse();
    }
}
