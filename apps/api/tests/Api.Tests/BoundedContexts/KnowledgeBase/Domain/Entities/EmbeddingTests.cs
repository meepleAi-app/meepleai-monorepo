using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Tests for the Embedding entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 18
/// </summary>
[Trait("Category", "Unit")]
public sealed class EmbeddingTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesEmbedding()
    {
        // Arrange
        var id = Guid.NewGuid();
        var vectorDocumentId = Guid.NewGuid();
        var textContent = "Sample text content";
        var vector = new Vector([1.0f, 0.0f, 0.0f]);
        var model = "text-embedding-ada-002";
        var chunkIndex = 0;
        var pageNumber = 1;

        // Act
        var embedding = new Embedding(id, vectorDocumentId, textContent, vector, model, chunkIndex, pageNumber);

        // Assert
        embedding.Id.Should().Be(id);
        embedding.VectorDocumentId.Should().Be(vectorDocumentId);
        embedding.TextContent.Should().Be(textContent);
        embedding.Vector.Should().Be(vector);
        embedding.Model.Should().Be(model);
        embedding.ChunkIndex.Should().Be(chunkIndex);
        embedding.PageNumber.Should().Be(pageNumber);
        embedding.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithEmptyTextContent_ThrowsArgumentException()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var action = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "", vector, "model", 0, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("textContent")
            .WithMessage("*Text content cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceTextContent_ThrowsArgumentException()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var action = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "   ", vector, "model", 0, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Text content cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyModel_ThrowsArgumentException()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var action = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "content", vector, "", 0, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("model")
            .WithMessage("*Model cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceModel_ThrowsArgumentException()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var action = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "content", vector, "   ", 0, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Model cannot be empty*");
    }

    [Fact]
    public void Constructor_WithNullVector_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "content", null!, "model", 0, 1);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("vector");
    }

    [Fact]
    public void Constructor_WithNegativeChunkIndex_ThrowsArgumentException()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var action = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "content", vector, "model", -1, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("chunkIndex")
            .WithMessage("*Chunk index must be non-negative*");
    }

    [Fact]
    public void Constructor_WithZeroChunkIndex_Succeeds()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var embedding = new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "content", vector, "model", 0, 1);

        // Assert
        embedding.ChunkIndex.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithZeroPageNumber_ThrowsArgumentException()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var action = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "content", vector, "model", 0, 0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("pageNumber")
            .WithMessage("*Page number must be positive*");
    }

    [Fact]
    public void Constructor_WithNegativePageNumber_ThrowsArgumentException()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);

        // Act
        var action = () => new Embedding(
            Guid.NewGuid(), Guid.NewGuid(), "content", vector, "model", 0, -1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Page number must be positive*");
    }

    #endregion

    #region CalculateSimilarity Tests

    [Fact]
    public void CalculateSimilarity_WithIdenticalVectors_ReturnsOne()
    {
        // Arrange
        var vector = new Vector([1.0f, 0.0f, 0.0f]);
        var embedding1 = CreateEmbedding(vector);
        var embedding2 = CreateEmbedding(vector);

        // Act
        var similarity = embedding1.CalculateSimilarity(embedding2);

        // Assert
        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CalculateSimilarity_WithOrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 0.0f, 0.0f]);
        var vector2 = new Vector([0.0f, 1.0f, 0.0f]);
        var embedding1 = CreateEmbedding(vector1);
        var embedding2 = CreateEmbedding(vector2);

        // Act
        var similarity = embedding1.CalculateSimilarity(embedding2);

        // Assert
        similarity.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void CalculateSimilarity_WithOppositeVectors_ReturnsNegativeOne()
    {
        // Arrange
        var vector1 = new Vector([1.0f, 0.0f, 0.0f]);
        var vector2 = new Vector([-1.0f, 0.0f, 0.0f]);
        var embedding1 = CreateEmbedding(vector1);
        var embedding2 = CreateEmbedding(vector2);

        // Act
        var similarity = embedding1.CalculateSimilarity(embedding2);

        // Assert
        similarity.Should().BeApproximately(-1.0, 0.001);
    }

    [Fact]
    public void CalculateSimilarity_WithSimilarVectors_ReturnsHighScore()
    {
        // Arrange
        var vector1 = new Vector([0.9f, 0.1f, 0.0f]);
        var vector2 = new Vector([0.8f, 0.2f, 0.0f]);
        var embedding1 = CreateEmbedding(vector1);
        var embedding2 = CreateEmbedding(vector2);

        // Act
        var similarity = embedding1.CalculateSimilarity(embedding2);

        // Assert
        similarity.Should().BeGreaterThan(0.9);
    }

    #endregion

    #region Helper Methods

    private static Embedding CreateEmbedding(Vector vector)
    {
        return new Embedding(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test content",
            vector,
            "test-model",
            0,
            1);
    }

    #endregion
}
