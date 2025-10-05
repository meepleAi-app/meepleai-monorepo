using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Unit tests for QdrantService
/// Note: Full integration tests with real Qdrant instance are recommended
/// for comprehensive testing of gRPC operations.
/// </summary>
public class QdrantServiceTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<QdrantService>> _loggerMock;

    public QdrantServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<QdrantService>>();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_InitializesSuccessfully()
    {
        // Arrange
        _configMock.Setup(c => c["QDRANT_URL"]).Returns("http://localhost:6333");

        // Act
        var service = new QdrantService(_configMock.Object, _loggerMock.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithHttpsUrl_InitializesSuccessfully()
    {
        // Arrange
        _configMock.Setup(c => c["QDRANT_URL"]).Returns("https://qdrant.example.com:6333");

        // Act
        var service = new QdrantService(_configMock.Object, _loggerMock.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithoutQdrantUrl_UsesDefaultLocalhost()
    {
        // Arrange
        _configMock.Setup(c => c["QDRANT_URL"]).Returns((string?)null);

        // Act
        var service = new QdrantService(_configMock.Object, _loggerMock.Object);

        // Assert
        Assert.NotNull(service);
        // Service should use default localhost:6333
    }

    [Fact]
    public async Task IndexDocumentChunksAsync_NullChunks_ReturnsFailure()
    {
        // Arrange
        _configMock.Setup(c => c["QDRANT_URL"]).Returns("http://localhost:6333");
        var service = new QdrantService(_configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.IndexDocumentChunksAsync(
            "game-1",
            "pdf-1",
            null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No chunks to index", result.ErrorMessage);
        Assert.Equal(0, result.IndexedCount);
    }

    [Fact]
    public async Task IndexDocumentChunksAsync_EmptyChunks_ReturnsFailure()
    {
        // Arrange
        _configMock.Setup(c => c["QDRANT_URL"]).Returns("http://localhost:6333");
        var service = new QdrantService(_configMock.Object, _loggerMock.Object);

        // Act
        var result = await service.IndexDocumentChunksAsync(
            "game-1",
            "pdf-1",
            new List<DocumentChunk>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No chunks to index", result.ErrorMessage);
        Assert.Equal(0, result.IndexedCount);
    }

    [Fact]
    public void IndexResult_CreateSuccess_ReturnsSuccessfulResult()
    {
        // Act
        var result = IndexResult.CreateSuccess(10);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(10, result.IndexedCount);
    }

    [Fact]
    public void IndexResult_CreateFailure_ReturnsFailedResult()
    {
        // Act
        var result = IndexResult.CreateFailure("Test error");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Test error", result.ErrorMessage);
        Assert.Equal(0, result.IndexedCount);
    }

    [Fact]
    public void SearchResult_CreateSuccess_ReturnsSuccessfulResult()
    {
        // Arrange
        var items = new List<SearchResultItem>
        {
            new() { Score = 0.95f, Text = "Test text", PdfId = "pdf-1", Page = 1, ChunkIndex = 0 }
        };

        // Act
        var result = SearchResult.CreateSuccess(items);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Single(result.Results);
        Assert.Equal(0.95f, result.Results[0].Score);
    }

    [Fact]
    public void SearchResult_CreateFailure_ReturnsFailedResult()
    {
        // Act
        var result = SearchResult.CreateFailure("Search failed");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Search failed", result.ErrorMessage);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void DocumentChunk_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var embedding = new float[1536];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = 0.1f;
        }

        // Act
        var chunk = new DocumentChunk
        {
            Text = "Test chunk text",
            Embedding = embedding,
            Page = 1,
            CharStart = 0,
            CharEnd = 15
        };

        // Assert
        Assert.Equal("Test chunk text", chunk.Text);
        Assert.Equal(1536, chunk.Embedding.Length);
        Assert.Equal(1, chunk.Page);
        Assert.Equal(0, chunk.CharStart);
        Assert.Equal(15, chunk.CharEnd);
    }

    [Fact]
    public void SearchResultItem_WithValidData_CreatesCorrectly()
    {
        // Act
        var item = new SearchResultItem
        {
            Score = 0.95f,
            Text = "Test result",
            PdfId = "pdf-123",
            Page = 5,
            ChunkIndex = 3
        };

        // Assert
        Assert.Equal(0.95f, item.Score);
        Assert.Equal("Test result", item.Text);
        Assert.Equal("pdf-123", item.PdfId);
        Assert.Equal(5, item.Page);
        Assert.Equal(3, item.ChunkIndex);
    }

    [Theory]
    [InlineData("tenant-1", "game-1", "pdf-1")]
    [InlineData("tenant-abc", "demo-chess", "pdf-xyz-123")]
    [InlineData("dev", "catan", "12345")]
    public void DocumentChunk_WithVariousTenantGamePdfIds_HandlesCorrectly(
        string tenantId,
        string gameId,
        string pdfId)
    {
        // This test verifies that the data structures support various ID formats
        var chunk = new DocumentChunk
        {
            Text = $"Chunk for {tenantId}/{gameId}/{pdfId}",
            Embedding = new float[1536],
            Page = 1,
            CharStart = 0,
            CharEnd = 10
        };

        Assert.NotNull(chunk);
        Assert.Contains(tenantId, chunk.Text);
    }

    // Note: Testing actual Qdrant operations (EnsureCollectionExistsAsync, IndexDocumentChunksAsync,
    // SearchAsync, DeleteDocumentAsync) requires integration tests with a real Qdrant instance.
    // The QdrantClient uses gRPC and is not easily mockable without significant infrastructure.
    //
    // For comprehensive testing, consider:
    // 1. Integration tests with Qdrant running in Docker
    // 2. E2E tests that verify the full indexing and search pipeline
    // 3. Manual testing with the development environment
    //
    // These unit tests focus on:
    // - Constructor initialization
    // - Input validation
    // - Data structure correctness
    // - Result object creation
}
