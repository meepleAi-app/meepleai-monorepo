using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: Unit tests for ChunkIndexEntry domain entity.
/// </summary>
public class ChunkIndexEntryTests
{
    private static readonly Guid TestGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestPdfId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Create_WithValidParameters_Succeeds()
    {
        // Arrange
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        var payload = ChunkPayload.Create(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0,
            level: 0);

        // Act
        var entry = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test content",
            vector: vector,
            payload: payload,
            embeddingModel: "text-embedding-3-large");

        // Assert
        Assert.NotNull(entry.Id);
        Assert.Equal("chunk-001", entry.ChunkId);
        Assert.Equal(TestGameId, entry.GameId);
        Assert.Equal(TestPdfId, entry.PdfDocumentId);
        Assert.Equal("Test content", entry.Content);
        Assert.Equal(vector, entry.Vector);
        Assert.Equal(payload, entry.Payload);
        Assert.Equal("text-embedding-3-large", entry.EmbeddingModel);
        Assert.True(entry.IndexedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        // Arrange
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        var payload = ChunkPayload.Empty();

        // Act
        var entry1 = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test 1",
            vector: vector,
            payload: payload);

        var entry2 = ChunkIndexEntry.Create(
            chunkId: "chunk-002",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test 2",
            vector: vector,
            payload: payload);

        // Assert
        Assert.NotEqual(entry1.Id, entry2.Id);
    }

    [Fact]
    public void Create_WithEmptyChunkId_ThrowsArgumentException()
    {
        // Arrange
        var vector = new float[] { 0.1f };
        var payload = ChunkPayload.Empty();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ChunkIndexEntry.Create(
                chunkId: "",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "Test",
                vector: vector,
                payload: payload));
    }

    [Fact]
    public void Create_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var vector = new float[] { 0.1f };
        var payload = ChunkPayload.Empty();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ChunkIndexEntry.Create(
                chunkId: "chunk-001",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "",
                vector: vector,
                payload: payload));
    }

    [Fact]
    public void Create_WithEmptyVector_ThrowsArgumentException()
    {
        // Arrange
        var payload = ChunkPayload.Empty();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ChunkIndexEntry.Create(
                chunkId: "chunk-001",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "Test",
                vector: Array.Empty<float>(),
                payload: payload));
    }

    [Fact]
    public void Create_WithNullPayload_ThrowsArgumentNullException()
    {
        // Arrange
        var vector = new float[] { 0.1f };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ChunkIndexEntry.Create(
                chunkId: "chunk-001",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "Test",
                vector: vector,
                payload: null!));
    }

    [Fact]
    public void UpdateVector_ChangesVectorAndTimestamp()
    {
        // Arrange
        var initialVector = new float[] { 0.1f, 0.2f };
        var payload = ChunkPayload.Empty();
        var entry = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test",
            vector: initialVector,
            payload: payload);

        var originalTimestamp = entry.IndexedAt;
        var newVector = new float[] { 0.5f, 0.6f };

        // Wait a bit to ensure timestamp difference
        Thread.Sleep(10);

        // Act
        entry.UpdateVector(newVector, "text-embedding-3-small");

        // Assert
        Assert.Equal(newVector, entry.Vector);
        Assert.Equal("text-embedding-3-small", entry.EmbeddingModel);
        Assert.True(entry.IndexedAt >= originalTimestamp);
    }

    [Fact]
    public void VectorDimensions_ReturnsCorrectDimension()
    {
        // Arrange
        var vector = new float[3072]; // text-embedding-3-large dimension
        Array.Fill(vector, 0.1f);
        var payload = ChunkPayload.Empty();

        // Act
        var entry = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test",
            vector: vector,
            payload: payload);

        // Assert
        Assert.Equal(3072, entry.VectorDimensions);
    }

    [Fact]
    public void IsParent_WithNoParentChunkId_ReturnsTrue()
    {
        // Arrange
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        var payload = ChunkPayload.CreateRoot(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0);

        // Act
        var entry = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test content",
            vector: vector,
            payload: payload);

        // Assert
        Assert.True(entry.IsParent);
    }

    [Fact]
    public void IsParent_WithParentChunkId_ReturnsFalse()
    {
        // Arrange
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        var payload = ChunkPayload.CreateChild(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0,
            level: 1,
            parentChunkId: "parent-001");

        // Act
        var entry = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test content",
            vector: vector,
            payload: payload);

        // Assert
        Assert.False(entry.IsParent);
    }

    [Fact]
    public void HasChildren_WithChildren_ReturnsTrue()
    {
        // Arrange
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        var payload = ChunkPayload.CreateRoot(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0,
            childChunkIds: new List<string> { "child-1", "child-2" });

        // Act
        var entry = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test content",
            vector: vector,
            payload: payload);

        // Assert
        Assert.True(entry.HasChildren);
    }

    [Fact]
    public void HasChildren_WithoutChildren_ReturnsFalse()
    {
        // Arrange
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        var payload = ChunkPayload.CreateRoot(
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            pageNumber: 1,
            chunkIndex: 0);

        // Act
        var entry = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test content",
            vector: vector,
            payload: payload);

        // Assert
        Assert.False(entry.HasChildren);
    }

    [Fact]
    public void DefaultEmbeddingModel_IsTextEmbedding3Large()
    {
        // Arrange
        var vector = new float[] { 0.1f };
        var payload = ChunkPayload.Empty();

        // Act
        var entry = ChunkIndexEntry.Create(
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test",
            vector: vector,
            payload: payload);

        // Assert
        Assert.Equal("text-embedding-3-large", entry.EmbeddingModel);
    }

    [Fact]
    public void CreateWithId_SetsSpecificId()
    {
        // Arrange
        var vector = new float[] { 0.1f };
        var payload = ChunkPayload.Empty();
        var specificId = "my-specific-id-12345";

        // Act
        var entry = ChunkIndexEntry.CreateWithId(
            id: specificId,
            chunkId: "chunk-001",
            gameId: TestGameId,
            pdfDocumentId: TestPdfId,
            content: "Test",
            vector: vector,
            payload: payload);

        // Assert
        Assert.Equal(specificId, entry.Id);
    }

    [Fact]
    public void CreateWithId_EmptyId_ThrowsArgumentException()
    {
        // Arrange
        var vector = new float[] { 0.1f };
        var payload = ChunkPayload.Empty();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ChunkIndexEntry.CreateWithId(
                id: "",
                chunkId: "chunk-001",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "Test",
                vector: vector,
                payload: payload));
    }
}
