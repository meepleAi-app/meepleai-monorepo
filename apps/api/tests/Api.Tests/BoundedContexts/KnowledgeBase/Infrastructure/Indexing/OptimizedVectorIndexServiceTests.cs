using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Indexing;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Qdrant.Client.Grpc;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Indexing;

/// <summary>
/// ADR-016 Phase 3: Unit tests for OptimizedVectorIndexService.
/// Tests HNSW and quantization configuration management.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class OptimizedVectorIndexServiceTests
{
    private readonly Mock<IQdrantClientAdapter> _mockClientAdapter;
    private readonly Mock<ILogger<OptimizedVectorIndexService>> _mockLogger;
    private readonly OptimizedVectorIndexService _service;

    public OptimizedVectorIndexServiceTests()
    {
        _mockClientAdapter = new Mock<IQdrantClientAdapter>();
        _mockLogger = new Mock<ILogger<OptimizedVectorIndexService>>();
        _service = new OptimizedVectorIndexService(_mockClientAdapter.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task EnsureOptimizedCollectionAsync_NewCollection_CreatesWithDefaultConfig()
    {
        // Arrange
        _mockClientAdapter
            .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockClientAdapter
            .Setup(x => x.CreateCollectionWithConfigAsync(
                It.IsAny<string>(),
                It.IsAny<VectorParams>(),
                It.IsAny<HnswConfigDiff>(),
                It.IsAny<QuantizationConfig?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockClientAdapter
            .Setup(x => x.CreatePayloadIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PayloadSchemaType>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.EnsureOptimizedCollectionAsync("test_collection");

        // Assert
        _mockClientAdapter.Verify(
            x => x.CreateCollectionWithConfigAsync(
                "test_collection",
                It.Is<VectorParams>(v => v.Size == 3072 && v.Distance == Distance.Cosine),
                It.Is<HnswConfigDiff>(h => h.M == 16 && h.EfConstruct == 100),
                It.Is<QuantizationConfig>(q => q.Scalar.Type == Qdrant.Client.Grpc.QuantizationType.Int8),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureOptimizedCollectionAsync_ExistingCollection_UpdatesConfig()
    {
        // Arrange
        _mockClientAdapter
            .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "test_collection" });

        _mockClientAdapter
            .Setup(x => x.UpdateCollectionConfigAsync(
                It.IsAny<string>(),
                It.IsAny<HnswConfigDiff>(),
                It.IsAny<QuantizationConfigDiff?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.EnsureOptimizedCollectionAsync("test_collection");

        // Assert
        _mockClientAdapter.Verify(
            x => x.UpdateCollectionConfigAsync(
                "test_collection",
                It.Is<HnswConfigDiff>(h => h.M == 16 && h.EfConstruct == 100),
                It.IsAny<QuantizationConfigDiff>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureOptimizedCollectionAsync_CustomVectorDimension_UsesProvided()
    {
        // Arrange
        _mockClientAdapter
            .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockClientAdapter
            .Setup(x => x.CreateCollectionWithConfigAsync(
                It.IsAny<string>(),
                It.IsAny<VectorParams>(),
                It.IsAny<HnswConfigDiff>(),
                It.IsAny<QuantizationConfig?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockClientAdapter
            .Setup(x => x.CreatePayloadIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PayloadSchemaType>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.EnsureOptimizedCollectionAsync("test_collection", vectorDimension: 1536);

        // Assert
        _mockClientAdapter.Verify(
            x => x.CreateCollectionWithConfigAsync(
                "test_collection",
                It.Is<VectorParams>(v => v.Size == 1536),
                It.IsAny<HnswConfigDiff>(),
                It.IsAny<QuantizationConfig?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureOptimizedCollectionAsync_CustomHnswConfig_UsesProvided()
    {
        // Arrange
        var hnswConfig = HnswConfiguration.HighAccuracy();

        _mockClientAdapter
            .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockClientAdapter
            .Setup(x => x.CreateCollectionWithConfigAsync(
                It.IsAny<string>(),
                It.IsAny<VectorParams>(),
                It.IsAny<HnswConfigDiff>(),
                It.IsAny<QuantizationConfig?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockClientAdapter
            .Setup(x => x.CreatePayloadIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PayloadSchemaType>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.EnsureOptimizedCollectionAsync("test_collection", hnswConfig: hnswConfig);

        // Assert
        _mockClientAdapter.Verify(
            x => x.CreateCollectionWithConfigAsync(
                "test_collection",
                It.IsAny<VectorParams>(),
                It.Is<HnswConfigDiff>(h => h.M == 24 && h.EfConstruct == 200),
                It.IsAny<QuantizationConfig?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureOptimizedCollectionAsync_DisabledQuantization_PassesNull()
    {
        // Arrange
        var quantConfig = QuantizationConfiguration.Disabled();

        _mockClientAdapter
            .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _mockClientAdapter
            .Setup(x => x.CreateCollectionWithConfigAsync(
                It.IsAny<string>(),
                It.IsAny<VectorParams>(),
                It.IsAny<HnswConfigDiff>(),
                It.IsAny<QuantizationConfig?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockClientAdapter
            .Setup(x => x.CreatePayloadIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PayloadSchemaType>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.EnsureOptimizedCollectionAsync(
            "test_collection",
            quantizationConfig: quantConfig);

        // Assert
        _mockClientAdapter.Verify(
            x => x.CreateCollectionWithConfigAsync(
                "test_collection",
                It.IsAny<VectorParams>(),
                It.IsAny<HnswConfigDiff>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOptimizedCollectionAsync_CreatesPayloadIndexes()
    {
        // Arrange
        _mockClientAdapter
            .Setup(x => x.CreateCollectionWithConfigAsync(
                It.IsAny<string>(),
                It.IsAny<VectorParams>(),
                It.IsAny<HnswConfigDiff>(),
                It.IsAny<QuantizationConfig?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockClientAdapter
            .Setup(x => x.CreatePayloadIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<PayloadSchemaType>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateOptimizedCollectionAsync(
            "test_collection",
            3072,
            HnswConfiguration.Default(),
            QuantizationConfiguration.Default());

        // Assert - Verify all payload indexes are created
        _mockClientAdapter.Verify(
            x => x.CreatePayloadIndexAsync("test_collection", "game_id", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockClientAdapter.Verify(
            x => x.CreatePayloadIndexAsync("test_collection", "pdf_id", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockClientAdapter.Verify(
            x => x.CreatePayloadIndexAsync("test_collection", "category", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockClientAdapter.Verify(
            x => x.CreatePayloadIndexAsync("test_collection", "language", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockClientAdapter.Verify(
            x => x.CreatePayloadIndexAsync("test_collection", "level", PayloadSchemaType.Integer, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockClientAdapter.Verify(
            x => x.CreatePayloadIndexAsync("test_collection", "parent_chunk_id", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockClientAdapter.Verify(
            x => x.CreatePayloadIndexAsync("test_collection", "page_number", PayloadSchemaType.Integer, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockClientAdapter.Verify(
            x => x.CreatePayloadIndexAsync("test_collection", "element_type", PayloadSchemaType.Keyword, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCollectionConfigurationAsync_UpdatesHnswAndQuantization()
    {
        // Arrange
        var hnswConfig = HnswConfiguration.Create(32, 200);
        var quantConfig = QuantizationConfiguration.HighAccuracy();

        _mockClientAdapter
            .Setup(x => x.UpdateCollectionConfigAsync(
                It.IsAny<string>(),
                It.IsAny<HnswConfigDiff>(),
                It.IsAny<QuantizationConfigDiff?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateCollectionConfigurationAsync(
            "test_collection",
            hnswConfig,
            quantConfig);

        // Assert
        _mockClientAdapter.Verify(
            x => x.UpdateCollectionConfigAsync(
                "test_collection",
                It.Is<HnswConfigDiff>(h => h.M == 32 && h.EfConstruct == 200),
                It.Is<QuantizationConfigDiff>(q => System.Math.Abs(q.Scalar.Quantile - 0.999f) < 1e-6f),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullClientAdapter_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OptimizedVectorIndexService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OptimizedVectorIndexService(_mockClientAdapter.Object, null!));
    }

    [Fact]
    public void DefaultVectorDimension_Is3072()
    {
        // Assert - ADR-016 Phase 3 specifies text-embedding-3-large with 3072 dimensions
        Assert.Equal(3072u, OptimizedVectorIndexService.DefaultVectorDimension);
    }

    [Fact]
    public void DefaultCollectionName_IsBoardgameRules()
    {
        // Assert
        Assert.Equal("boardgame_rules", OptimizedVectorIndexService.DefaultCollectionName);
    }
}
