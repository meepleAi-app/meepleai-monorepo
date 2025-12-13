using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;

/// <summary>
/// Unit tests for EmbeddingService with multi-provider abstraction.
/// ADR-016 Phase 2: Tests the refactored EmbeddingService that uses IEmbeddingProviderFactory.
/// Tests embedding generation, fallback behavior, and error handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EmbeddingServiceTests
{
    private readonly Mock<IEmbeddingProviderFactory> _providerFactoryMock;
    private readonly Mock<IEmbeddingProvider> _primaryProviderMock;
    private readonly Mock<IEmbeddingProvider> _fallbackProviderMock;
    private readonly Mock<ILogger<EmbeddingService>> _loggerMock;
    private readonly EmbeddingConfiguration _defaultConfig;

    public EmbeddingServiceTests()
    {
        _providerFactoryMock = new Mock<IEmbeddingProviderFactory>();
        _primaryProviderMock = new Mock<IEmbeddingProvider>();
        _fallbackProviderMock = new Mock<IEmbeddingProvider>();
        _loggerMock = new Mock<ILogger<EmbeddingService>>();

        _defaultConfig = new EmbeddingConfiguration
        {
            Provider = EmbeddingProviderType.OllamaNomic,
            EnableFallback = true,
            BatchSize = 10
        };

        // Setup default provider mocks
        _primaryProviderMock.Setup(x => x.ProviderName).Returns("Primary");
        _primaryProviderMock.Setup(x => x.ModelName).Returns("test-model");
        _primaryProviderMock.Setup(x => x.Dimensions).Returns(768);

        _fallbackProviderMock.Setup(x => x.ProviderName).Returns("Fallback");
        _fallbackProviderMock.Setup(x => x.ModelName).Returns("fallback-model");
        _fallbackProviderMock.Setup(x => x.Dimensions).Returns(768);

        _providerFactoryMock.Setup(x => x.GetPrimaryProvider()).Returns(_primaryProviderMock.Object);
        _providerFactoryMock.Setup(x => x.GetFallbackProvider()).Returns(_fallbackProviderMock.Object);
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var options = Options.Create(_defaultConfig);

        // Act
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullProviderFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_defaultConfig);

        // Act
        Action act = () => new EmbeddingService(
            null!,
            options,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("providerFactory");
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new EmbeddingService(
            _providerFactoryMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_defaultConfig);

        // Act
        Action act = () => new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
    [Fact]
    public void GetEmbeddingDimensions_ReturnsPrimaryProviderDimensions()
    {
        // Arrange
        _primaryProviderMock.Setup(x => x.Dimensions).Returns(1024);
        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var dimensions = service.GetEmbeddingDimensions();

        // Assert
        dimensions.Should().Be(1024);
    }
    [Fact]
    public void GetModelName_ReturnsCombinedProviderAndModelName()
    {
        // Arrange
        _primaryProviderMock.Setup(x => x.ProviderName).Returns("OpenRouter");
        _primaryProviderMock.Setup(x => x.ModelName).Returns("text-embedding-3-large");
        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var modelName = service.GetModelName();

        // Assert
        modelName.Should().Be("openrouter/text-embedding-3-large");
    }
    [Fact]
    public async Task GenerateEmbeddingsAsync_WithEmptyList_ReturnsFailure()
    {
        // Arrange
        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(new List<string>());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No texts provided");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithNullList_ReturnsFailure()
    {
        // Arrange
        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(null!);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No texts provided");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenPrimarySucceeds_ReturnsEmbeddings()
    {
        // Arrange
        var texts = new List<string> { "Hello world", "Test text" };
        var expectedEmbeddings = new List<float[]>
        {
            new float[] { 0.1f, 0.2f, 0.3f },
            new float[] { 0.4f, 0.5f, 0.6f }
        };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateSuccess(expectedEmbeddings, "test-model"));

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().HaveCount(2);
        result.Embeddings[0].Should().BeEquivalentTo(expectedEmbeddings[0]);
        result.Embeddings[1].Should().BeEquivalentTo(expectedEmbeddings[1]);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenPrimaryFails_TriesFallback()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var fallbackEmbeddings = new List<float[]>
        {
            new float[] { 0.7f, 0.8f, 0.9f }
        };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateFailure("Primary failed"));

        _fallbackProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateSuccess(fallbackEmbeddings, "fallback-model"));

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().HaveCount(1);
        _fallbackProviderMock.Verify(
            x => x.GenerateBatchEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenFallbackDisabled_DoesNotTryFallback()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var config = new EmbeddingConfiguration
        {
            Provider = EmbeddingProviderType.OllamaNomic,
            EnableFallback = false
        };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateFailure("Primary failed"));

        var options = Options.Create(config);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Primary failed");
        _fallbackProviderMock.Verify(
            x => x.GenerateBatchEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenNoFallbackConfigured_ReturnsFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };

        _providerFactoryMock.Setup(x => x.GetFallbackProvider()).Returns((IEmbeddingProvider?)null);

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateFailure("Primary failed"));

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Primary failed");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenBothProvidersFail_ReturnsFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateFailure("Primary failed"));

        _fallbackProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateFailure("Fallback failed"));

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Primary failed");
    }
    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidText_ReturnsEmbedding()
    {
        // Arrange
        var text = "Hello world";
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateSuccess(new List<float[]> { expectedEmbedding }, "test-model"));

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync(text);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().HaveCount(1);
        result.Embeddings[0].Should().BeEquivalentTo(expectedEmbedding);
    }
    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    public async Task GenerateEmbeddingsAsync_WithValidLanguage_SucceedsWithoutValidationError(string language)
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var expectedEmbeddings = new List<float[]> { new float[] { 0.1f, 0.2f } };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateSuccess(expectedEmbeddings, "test-model"));

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts, language);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithUnsupportedLanguage_FallsBackToEnglish()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var expectedEmbeddings = new List<float[]> { new float[] { 0.1f, 0.2f } };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateSuccess(expectedEmbeddings, "test-model"));

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts, "unsupported-lang");

        // Assert
        result.Success.Should().BeTrue();
        // The service should have logged a warning and continued
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithLanguage_CallsCorrectOverload()
    {
        // Arrange
        var text = "Ciao mondo";
        var language = "it";
        var expectedEmbeddings = new List<float[]> { new float[] { 0.1f, 0.2f } };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingProviderResult.CreateSuccess(expectedEmbeddings, "test-model"));

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync(text, language);

        // Assert
        result.Success.Should().BeTrue();
    }
    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenProviderThrowsException_ReturnsFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        _providerFactoryMock.Setup(x => x.GetFallbackProvider()).Returns((IEmbeddingProvider?)null);

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenCancelled_ReturnsFailure()
    {
        // Arrange
        var texts = new List<string> { "Test text" };
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        _providerFactoryMock.Setup(x => x.GetFallbackProvider()).Returns((IEmbeddingProvider?)null);

        var options = Options.Create(_defaultConfig);
        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            options,
            _loggerMock.Object);

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts, cts.Token);

        // Assert
        result.Success.Should().BeFalse();
    }
}
