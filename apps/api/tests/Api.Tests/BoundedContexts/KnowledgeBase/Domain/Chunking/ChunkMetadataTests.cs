using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Chunking;

/// <summary>
/// Unit tests for ChunkMetadata value object.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ChunkMetadataTests
{
    [Fact]
    public void Create_WithAllProperties_SetsCorrectly()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var metadata = new ChunkMetadata
        {
            Page = 5,
            Heading = "Combat Rules",
            ElementType = "table",
            GameId = gameId,
            DocumentId = documentId,
            CharStart = 1000,
            CharEnd = 1500,
            BBox = new BoundingBox { X = 10, Y = 20, Width = 100, Height = 50 }
        };

        // Assert
        metadata.Page.Should().Be(5);
        metadata.Heading.Should().Be("Combat Rules");
        metadata.ElementType.Should().Be("table");
        metadata.GameId.Should().Be(gameId);
        metadata.DocumentId.Should().Be(documentId);
        metadata.CharStart.Should().Be(1000);
        metadata.CharEnd.Should().Be(1500);
        metadata.BBox.Should().NotBeNull();
        metadata.BBox!.X.Should().Be(10);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var metadata = new ChunkMetadata();

        // Assert
        metadata.Page.Should().Be(0);
        metadata.Heading.Should().BeNull();
        metadata.ElementType.Should().Be("text");
        metadata.GameId.Should().BeNull();
        metadata.DocumentId.Should().Be(Guid.Empty);
        metadata.CharStart.Should().Be(0);
        metadata.CharEnd.Should().Be(0);
        metadata.BBox.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var metadata1 = new ChunkMetadata
        {
            Page = 1,
            DocumentId = documentId,
            Heading = "Test"
        };
        var metadata2 = new ChunkMetadata
        {
            Page = 1,
            DocumentId = documentId,
            Heading = "Test"
        };

        // Assert
        metadata1.Should().Be(metadata2);
        (metadata1 == metadata2).Should().BeTrue();
    }

    [Fact]
    public void RecordInequality_WithDifferentPage_Works()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var metadata1 = new ChunkMetadata { Page = 1, DocumentId = documentId };
        var metadata2 = new ChunkMetadata { Page = 2, DocumentId = documentId };

        // Assert
        metadata1.Should().NotBe(metadata2);
        (metadata1 != metadata2).Should().BeTrue();
    }
}

/// <summary>
/// Unit tests for BoundingBox value object.
/// </summary>
public class BoundingBoxTests
{
    [Fact]
    public void FromCoordinates_CreatesCorrectBoundingBox()
    {
        // Act
        var bbox = BoundingBox.FromCoordinates(10.5f, 20.3f, 100.0f, 50.0f);

        // Assert
        bbox.X.Should().Be(10.5f);
        bbox.Y.Should().Be(20.3f);
        bbox.Width.Should().Be(100.0f);
        bbox.Height.Should().Be(50.0f);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        // Arrange
        var bbox1 = new BoundingBox { X = 10, Y = 20, Width = 100, Height = 50 };
        var bbox2 = new BoundingBox { X = 10, Y = 20, Width = 100, Height = 50 };

        // Assert
        bbox1.Should().Be(bbox2);
        (bbox1 == bbox2).Should().BeTrue();
    }
}
