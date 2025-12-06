using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Chunking;

/// <summary>
/// Unit tests for HierarchicalChunk entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class HierarchicalChunkTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsChunk()
    {
        // Arrange
        var content = "This is test content for chunking.";
        var metadata = new ChunkMetadata
        {
            Page = 1,
            DocumentId = Guid.NewGuid(),
            ElementType = "text"
        };

        // Act
        var chunk = HierarchicalChunk.Create(content, level: 1, metadata);

        // Assert
        chunk.Should().NotBeNull();
        chunk.Id.Should().NotBeNullOrEmpty();
        chunk.Content.Should().Be(content);
        chunk.Level.Should().Be(1);
        chunk.Metadata.Should().Be(metadata);
        chunk.ParentId.Should().BeNull();
        chunk.ChildIds.Should().BeEmpty();
        chunk.IsRoot.Should().BeTrue();
        chunk.HasChildren.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var metadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };

        // Act
        var act = () => HierarchicalChunk.Create("", level: 0, metadata);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceContent_ThrowsArgumentException()
    {
        // Arrange
        var metadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };

        // Act
        var act = () => HierarchicalChunk.Create("   ", level: 0, metadata);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Content cannot be empty*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(10)]
    public void Create_WithInvalidLevel_ThrowsArgumentOutOfRangeException(int invalidLevel)
    {
        // Arrange
        var metadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };

        // Act
        var act = () => HierarchicalChunk.Create("content", invalidLevel, metadata);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Level must be 0, 1, or 2*");
    }

    [Fact]
    public void CreateParent_CreatesRootChunkWithLevel0()
    {
        // Arrange
        var content = "Section content";
        var metadata = new ChunkMetadata
        {
            Page = 1,
            DocumentId = Guid.NewGuid(),
            Heading = "Introduction"
        };

        // Act
        var parent = HierarchicalChunk.CreateParent(content, metadata);

        // Assert
        parent.Level.Should().Be(0);
        parent.ParentId.Should().BeNull();
        parent.IsRoot.Should().BeTrue();
    }

    [Fact]
    public void CreateChild_CreatesChunkWithParentId()
    {
        // Arrange
        var parentMetadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };
        var parent = HierarchicalChunk.CreateParent("Parent content", parentMetadata);

        var childMetadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };

        // Act
        var child = HierarchicalChunk.CreateChild("Child content", level: 2, childMetadata, parent.Id);

        // Assert
        child.ParentId.Should().Be(parent.Id);
        child.Level.Should().Be(2);
        child.IsRoot.Should().BeFalse();
    }

    [Fact]
    public void CreateChild_WithEmptyParentId_ThrowsArgumentException()
    {
        // Arrange
        var metadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };

        // Act
        var act = () => HierarchicalChunk.CreateChild("content", level: 1, metadata, "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ParentId is required*");
    }

    [Fact]
    public void AddChild_AddsChildIdToList()
    {
        // Arrange
        var metadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };
        var parent = HierarchicalChunk.CreateParent("Parent content", metadata);
        var childId = Guid.NewGuid().ToString("N");

        // Act
        parent.AddChild(childId);

        // Assert
        parent.ChildIds.Should().Contain(childId);
        parent.HasChildren.Should().BeTrue();
    }

    [Fact]
    public void AddChild_WithDuplicateId_DoesNotAddDuplicate()
    {
        // Arrange
        var metadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };
        var parent = HierarchicalChunk.CreateParent("Parent content", metadata);
        var childId = Guid.NewGuid().ToString("N");

        // Act
        parent.AddChild(childId);
        parent.AddChild(childId); // Add same ID again

        // Assert
        parent.ChildIds.Should().HaveCount(1);
    }

    [Fact]
    public void AddChild_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var metadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };
        var parent = HierarchicalChunk.CreateParent("Parent content", metadata);

        // Act
        var act = () => parent.AddChild("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ChildId cannot be empty*");
    }

    [Fact]
    public void EstimatedTokenCount_CalculatesCorrectly()
    {
        // Arrange
        var content = "This is a test content with approximately 40 characters.";
        var metadata = new ChunkMetadata { DocumentId = Guid.NewGuid() };
        var chunk = HierarchicalChunk.Create(content, level: 0, metadata);

        // Act
        var tokenCount = chunk.EstimatedTokenCount;

        // Assert
        // 56 chars / 4 = 14 tokens (ceiling)
        tokenCount.Should().Be((int)Math.Ceiling(content.Length / 4.0));
    }
}
