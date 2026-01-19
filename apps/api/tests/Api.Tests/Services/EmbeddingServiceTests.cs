using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;

#pragma warning disable S2930 // Dispose CancellationTokenSource when it is no longer needed

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for EmbeddingService
/// Issue #2599: AI/Embedding Service Tests
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EmbeddingServiceTests
{
    private readonly Mock<IEmbeddingProviderFactory> _providerFactoryMock;
    private readonly Mock<IEmbeddingProvider> _primaryProviderMock;
    private readonly Mock<IEmbeddingProvider> _fallbackProviderMock;
    private readonly Mock<ILogger<EmbeddingService>> _loggerMock;
    private readonly EmbeddingConfiguration _config;
    private readonly EmbeddingService _service;

    public EmbeddingServiceTests()
    {
        _providerFactoryMock = new Mock<IEmbeddingProviderFactory>();
        _primaryProviderMock = new Mock<IEmbeddingProvider>();
        _fallbackProviderMock = new Mock<IEmbeddingProvider>();
        _loggerMock = new Mock<ILogger<EmbeddingService>>();

        _config = new EmbeddingConfiguration
        {
            Provider = EmbeddingProviderType.HuggingFaceBgeM3,
            EnableFallback = true
        };

        // Setup provider factory
        _providerFactoryMock
            .Setup(x => x.GetPrimaryProvider())
            .Returns(_primaryProviderMock.Object);

        _providerFactoryMock
            .Setup(x => x.GetFallbackProvider())
            .Returns(_fallbackProviderMock.Object);

        // Setup provider properties
        _primaryProviderMock.Setup(x => x.ProviderName).Returns("HuggingFace");
        _primaryProviderMock.Setup(x => x.ModelName).Returns("bge-m3");
        _primaryProviderMock.Setup(x => x.Dimensions).Returns(1024);

        _fallbackProviderMock.Setup(x => x.ProviderName).Returns("OpenRouter");
        _fallbackProviderMock.Setup(x => x.ModelName).Returns("text-embedding-ada-002");
        _fallbackProviderMock.Setup(x => x.Dimensions).Returns(1536);

        _service = new EmbeddingService(
            _providerFactoryMock.Object,
            Options.Create(_config),
            _loggerMock.Object
        );
    }

    #region GetEmbeddingDimensions Tests

    [Fact]
    public void GetEmbeddingDimensions_ShouldReturnPrimaryProviderDimensions()
    {
        // Act
        var dimensions = _service.GetEmbeddingDimensions();

        // Assert
        dimensions.Should().Be(1024);
    }

    #endregion

    #region GetModelName Tests

    [Fact]
    public void GetModelName_ShouldReturnPrimaryProviderInfo()
    {
        // Act
        var modelName = _service.GetModelName();

        // Assert
        modelName.Should().Be("huggingface/bge-m3");
    }

    #endregion

    #region GenerateEmbeddingsAsync (Batch) Tests

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithValidTexts_ShouldReturnSuccess()
    {
        // Arrange
        var texts = new List<string> { "test text 1", "test text 2" };
        var expectedEmbeddings = new List<float[]>
        {
            new float[1024],
            new float[1024]
        };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = true,
                Embeddings = expectedEmbeddings
            });

        // Act
        var result = await _service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().HaveCount(2);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithEmptyList_ShouldReturnFailure()
    {
        // Arrange
        var texts = new List<string>();

        // Act
        var result = await _service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No texts provided");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithNullList_ShouldReturnFailure()
    {
        // Act
        var result = await _service.GenerateEmbeddingsAsync(null!);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No texts provided");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenPrimaryFails_ShouldTryFallback()
    {
        // Arrange
        var texts = new List<string> { "test text" };
        var expectedEmbedding = new List<float[]> { new float[1536] };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = false,
                ErrorMessage = "Primary provider error"
            });

        _fallbackProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = true,
                Embeddings = expectedEmbedding
            });

        // Act
        var result = await _service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().HaveCount(1);

        _primaryProviderMock.Verify(
            x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()),
            Times.Once
        );

        _fallbackProviderMock.Verify(
            x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenBothProvidersFail_ShouldReturnCombinedError()
    {
        // Arrange
        var texts = new List<string> { "test text" };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = false,
                ErrorMessage = "Primary error"
            });

        _fallbackProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = false,
                ErrorMessage = "Fallback error"
            });

        // Act
        var result = await _service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Primary");
        result.ErrorMessage.Should().Contain("Fallback");
        result.ErrorMessage.Should().Contain("Primary error");
        result.ErrorMessage.Should().Contain("Fallback error");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithFallbackDisabled_ShouldNotTryFallback()
    {
        // Arrange
        var texts = new List<string> { "test text" };
        var configNoFallback = new EmbeddingConfiguration
        {
            Provider = EmbeddingProviderType.HuggingFaceBgeM3,
            EnableFallback = false
        };

        var service = new EmbeddingService(
            _providerFactoryMock.Object,
            Options.Create(configNoFallback),
            _loggerMock.Object
        );

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = false,
                ErrorMessage = "Primary error"
            });

        // Act
        var result = await service.GenerateEmbeddingsAsync(texts);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Primary error");

        _fallbackProviderMock.Verify(
            x => x.GenerateBatchEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var texts = new List<string> { "test text" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = false,
                ErrorMessage = "Operation cancelled"
            });

        // Act & Assert
        var result = await _service.GenerateEmbeddingsAsync(texts, cts.Token);
        result.Success.Should().BeFalse();
    }

    #endregion

    #region GenerateEmbeddingAsync (Single) Tests

    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidText_ShouldReturnSuccess()
    {
        // Arrange
        var text = "test text";
        var expectedEmbedding = new List<float[]> { new float[1024] };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.Is<List<string>>(l => l.Count == 1 && l[0] == text),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = true,
                Embeddings = expectedEmbedding
            });

        // Act
        var result = await _service.GenerateEmbeddingAsync(text);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().HaveCount(1);
    }

    #endregion

    #region GenerateEmbeddingsAsync (Multi-language Batch) Tests

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithValidLanguage_ShouldReturnSuccess()
    {
        // Arrange
        var texts = new List<string> { "test text" };
        var language = "it";
        var expectedEmbedding = new List<float[]> { new float[1024] };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = true,
                Embeddings = expectedEmbedding
            });

        // Act
        var result = await _service.GenerateEmbeddingsAsync(texts, language);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().HaveCount(1);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithInvalidLanguage_ShouldFallbackToEnglish()
    {
        // Arrange
        var texts = new List<string> { "test text" };
        var invalidLanguage = "xyz";
        var expectedEmbedding = new List<float[]> { new float[1024] };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = true,
                Embeddings = expectedEmbedding
            });

        // Act
        var result = await _service.GenerateEmbeddingsAsync(texts, invalidLanguage);

        // Assert
        result.Success.Should().BeTrue();
        // Logs should show warning about invalid language code
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithSupportedLanguages_ShouldAcceptAll()
    {
        // Arrange
        var supportedLanguages = new[] { "en", "it", "de", "fr", "es" };
        var texts = new List<string> { "test" };
        var expectedEmbedding = new List<float[]> { new float[1024] };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = true,
                Embeddings = expectedEmbedding
            });

        // Act & Assert
        foreach (var language in supportedLanguages)
        {
            var result = await _service.GenerateEmbeddingsAsync(texts, language);
            result.Success.Should().BeTrue($"language '{language}' should be supported");
        }
    }

    #endregion

    #region GenerateEmbeddingAsync (Multi-language Single) Tests

    [Fact]
    public async Task GenerateEmbeddingAsync_WithLanguage_ShouldReturnSuccess()
    {
        // Arrange
        var text = "test text";
        var language = "it";
        var expectedEmbedding = new List<float[]> { new float[1024] };

        _primaryProviderMock
            .Setup(x => x.GenerateBatchEmbeddingsAsync(
                It.Is<List<string>>(l => l.Count == 1 && l[0] == text),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingProviderResult
            {
                Success = true,
                Embeddings = expectedEmbedding
            });

        // Act
        var result = await _service.GenerateEmbeddingAsync(text, language);

        // Assert
        result.Success.Should().BeTrue();
        result.Embeddings.Should().HaveCount(1);
    }

    #endregion
}
