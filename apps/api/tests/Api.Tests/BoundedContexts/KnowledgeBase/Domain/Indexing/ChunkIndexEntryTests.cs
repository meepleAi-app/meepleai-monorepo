using Api.BoundedContexts.KnowledgeBase.Domain.Indexing;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Indexing;

/// <summary>
/// ADR-016 Phase 3: Unit tests for ChunkIndexEntry domain entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        entry.Id.Should().NotBeNull();
        entry.ChunkId.Should().Be("chunk-001");
        entry.GameId.Should().Be(TestGameId);
        entry.PdfDocumentId.Should().Be(TestPdfId);
        entry.Content.Should().Be("Test content");
        entry.Vector.Should().BeEquivalentTo(vector);
        entry.Payload.Should().Be(payload);
        entry.EmbeddingModel.Should().Be("text-embedding-3-large");
        (entry.IndexedAt <= DateTime.UtcNow).Should().BeTrue();
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
        entry2.Id.Should().NotBe(entry1.Id);
    }

    [Fact]
    public void Create_WithEmptyChunkId_ThrowsArgumentException()
    {
        // Arrange
        var vector = new float[] { 0.1f };
        var payload = ChunkPayload.Empty();

        // Act & Assert
        Action act = () =>
            ChunkIndexEntry.Create(
                chunkId: "",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "Test",
                vector: vector,
                payload: payload);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var vector = new float[] { 0.1f };
        var payload = ChunkPayload.Empty();

        // Act & Assert
        Action act = () =>
            ChunkIndexEntry.Create(
                chunkId: "chunk-001",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "",
                vector: vector,
                payload: payload);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyVector_ThrowsArgumentException()
    {
        // Arrange
        var payload = ChunkPayload.Empty();

        // Act & Assert
        Action act = () =>
            ChunkIndexEntry.Create(
                chunkId: "chunk-001",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "Test",
                vector: Array.Empty<float>(),
                payload: payload);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullPayload_ThrowsArgumentNullException()
    {
        // Arrange
        var vector = new float[] { 0.1f };

        // Act & Assert
        Action act = () =>
            ChunkIndexEntry.Create(
                chunkId: "chunk-001",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "Test",
                vector: vector,
                payload: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateVector_ChangesVectorAndTimestamp()
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
        await Task.Delay(TestConstants.Timing.TinyDelay);

        // Act
        entry.UpdateVector(newVector, "text-embedding-3-small");

        // Assert
        entry.Vector.Should().BeEquivalentTo(newVector);
        entry.EmbeddingModel.Should().Be("text-embedding-3-small");
        (entry.IndexedAt >= originalTimestamp).Should().BeTrue();
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
        entry.VectorDimensions.Should().Be(3072);
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
        entry.IsParent.Should().BeTrue();
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
        entry.IsParent.Should().BeFalse();
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
        entry.HasChildren.Should().BeTrue();
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
        entry.HasChildren.Should().BeFalse();
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
        entry.EmbeddingModel.Should().Be("text-embedding-3-large");
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
        entry.Id.Should().Be(specificId);
    }

    [Fact]
    public void CreateWithId_EmptyId_ThrowsArgumentException()
    {
        // Arrange
        var vector = new float[] { 0.1f };
        var payload = ChunkPayload.Empty();

        // Act & Assert
        Action act = () =>
            ChunkIndexEntry.CreateWithId(
                id: "",
                chunkId: "chunk-001",
                gameId: TestGameId,
                pdfDocumentId: TestPdfId,
                content: "Test",
                vector: vector,
                payload: payload);
        act.Should().Throw<ArgumentException>();
    }
}
