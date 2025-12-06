using Api.Services;
using Api.Services.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services.Rag;

/// <summary>
/// Unit tests for RagConfigurationProvider (Issue #1441).
/// Verifies 3-tier configuration fallback: Database → appsettings.json → Hardcoded defaults
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RagConfigurationProviderTests
{
    private readonly Mock<ILogger<RagConfigurationProvider>> _mockLogger;

    public RagConfigurationProviderTests()
    {
        _mockLogger = new Mock<ILogger<RagConfigurationProvider>>();
    }

    [Fact]
    public async Task GetRagConfigAsync_WithDatabaseConfig_ReturnsDatabaseValues()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.TopK", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(10);

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var topK = await provider.GetRagConfigAsync("TopK", 5);

        // Assert
        Assert.Equal(10, topK);
    }

    [Fact]
    public async Task GetRagConfigAsync_DatabaseConfigOverridesAppsettings()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"RAG:TopK", "8"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.TopK", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(15); // Database value should win

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object,
            configuration);

        // Act
        var topK = await provider.GetRagConfigAsync("TopK", 5);

        // Assert
        Assert.Equal(15, topK); // Database value, not appsettings
    }

    [Fact]
    public async Task GetRagConfigAsync_WithAppsettingsConfig_ReturnsAppsettingsValues()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"RAG:TopK", "12"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            configuration: configuration);

        // Act
        var topK = await provider.GetRagConfigAsync("TopK", 5);

        // Assert
        Assert.Equal(12, topK);
    }

    [Fact]
    public async Task GetRagConfigAsync_NoConfigSources_ReturnsHardcodedDefaults()
    {
        // Arrange
        var provider = new RagConfigurationProvider(_mockLogger.Object);

        // Act
        var topK = await provider.GetRagConfigAsync("TopK", 5);
        var minScore = await provider.GetRagConfigAsync("MinScore", 0.7);

        // Assert
        Assert.Equal(5, topK); // Hardcoded default
        Assert.Equal(0.7, minScore); // Hardcoded default
    }

    [Fact]
    public async Task GetRagConfigAsync_InvalidTopK_ClampsToValidRange()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.TopK", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(150); // Invalid: > 50

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var topK = await provider.GetRagConfigAsync("TopK", 5);

        // Assert
        Assert.Equal(50, topK); // Clamped to max
        Assert.True(topK >= 1 && topK <= 50);
    }

    [Fact]
    public async Task GetRagConfigAsync_InvalidTopKTooLow_ClampsToValidRange()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.TopK", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(0); // Invalid: < 1

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var topK = await provider.GetRagConfigAsync("TopK", 5);

        // Assert
        Assert.Equal(1, topK); // Clamped to min
        Assert.True(topK >= 1 && topK <= 50);
    }

    [Fact]
    public async Task GetRagConfigAsync_InvalidMinScore_ClampsToValidRange()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<double?>("RAG.MinScore", It.IsAny<double?>(), It.IsAny<string?>()))
            .ReturnsAsync(1.5); // Invalid: > 1.0

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var minScore = await provider.GetRagConfigAsync("MinScore", 0.7);

        // Assert
        Assert.Equal(1.0, minScore); // Clamped to max
        Assert.True(minScore >= 0.0 && minScore <= 1.0);
    }

    [Fact]
    public async Task GetRagConfigAsync_InvalidMinScoreTooLow_ClampsToValidRange()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<double?>("RAG.MinScore", It.IsAny<double?>(), It.IsAny<string?>()))
            .ReturnsAsync(-0.5); // Invalid: < 0.0

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var minScore = await provider.GetRagConfigAsync("MinScore", 0.7);

        // Assert
        Assert.Equal(0.0, minScore); // Clamped to min
        Assert.True(minScore >= 0.0 && minScore <= 1.0);
    }

    [Fact]
    public async Task GetRagConfigAsync_NullDatabaseValue_FallsBackToAppsettings()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"RAG:TopK", "7"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.TopK", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync((int?)null); // Database returns null

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object,
            configuration);

        // Act
        var topK = await provider.GetRagConfigAsync("TopK", 5);

        // Assert
        Assert.Equal(7, topK); // Appsettings value
    }

    [Fact]
    public async Task GetRagConfigAsync_NullConfigurationValue_FallsBackToDefaults()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"RAG:SomeOtherKey", "value"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            configuration: configuration);

        // Act
        var topK = await provider.GetRagConfigAsync("TopK", 5);

        // Assert
        Assert.Equal(5, topK); // Default value
    }

    [Fact]
    public async Task GetRagConfigAsync_ValidRrfK_ReturnsValue()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.RrfK", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(60);

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var rrfK = await provider.GetRagConfigAsync("RrfK", 60);

        // Assert
        Assert.Equal(60, rrfK);
        Assert.True(rrfK >= 1 && rrfK <= 100);
    }

    [Fact]
    public async Task GetRagConfigAsync_InvalidRrfK_ClampsToValidRange()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.RrfK", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(150); // Invalid: > 100

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var rrfK = await provider.GetRagConfigAsync("RrfK", 60);

        // Assert
        Assert.Equal(100, rrfK); // Clamped to max
    }

    [Fact]
    public async Task GetRagConfigAsync_ValidMaxQueryVariations_ReturnsValue()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.MaxQueryVariations", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(5);

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var maxVariations = await provider.GetRagConfigAsync("MaxQueryVariations", 3);

        // Assert
        Assert.Equal(5, maxVariations);
        Assert.True(maxVariations >= 1 && maxVariations <= 10);
    }

    [Fact]
    public async Task GetRagConfigAsync_InvalidMaxQueryVariations_ClampsToValidRange()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.MaxQueryVariations", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(20); // Invalid: > 10

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var maxVariations = await provider.GetRagConfigAsync("MaxQueryVariations", 3);

        // Assert
        Assert.Equal(10, maxVariations); // Clamped to max
    }

    [Fact]
    public async Task GetRagConfigAsync_UnknownConfigKey_ReturnsValueWithoutValidation()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("RAG.UnknownKey", It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(999);

        var provider = new RagConfigurationProvider(
            _mockLogger.Object,
            mockConfigService.Object);

        // Act
        var value = await provider.GetRagConfigAsync("UnknownKey", 100);

        // Assert
        Assert.Equal(999, value); // No clamping for unknown keys
    }
}

