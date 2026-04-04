using Api.Services;
using Api.Services.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
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
        topK.Should().Be(10);
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
        topK.Should().Be(15);
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
        topK.Should().Be(12);
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
        topK.Should().Be(5);
        minScore.Should().Be(0.7);
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
        topK.Should().Be(50);
        (topK >= 1 && topK <= 50).Should().BeTrue();
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
        topK.Should().Be(1);
        (topK >= 1 && topK <= 50).Should().BeTrue();
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
        minScore.Should().Be(1.0);
        (minScore >= 0.0 && minScore <= 1.0).Should().BeTrue();
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
        minScore.Should().Be(0.0);
        (minScore >= 0.0 && minScore <= 1.0).Should().BeTrue();
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
        topK.Should().Be(7);
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
        topK.Should().Be(5);
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
        rrfK.Should().Be(60);
        (rrfK >= 1 && rrfK <= 100).Should().BeTrue();
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
        rrfK.Should().Be(100);
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
        maxVariations.Should().Be(5);
        (maxVariations >= 1 && maxVariations <= 10).Should().BeTrue();
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
        maxVariations.Should().Be(10);
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
        value.Should().Be(999);
    }
}

