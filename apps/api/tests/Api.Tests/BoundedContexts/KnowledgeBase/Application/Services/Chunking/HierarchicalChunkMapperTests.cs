using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// Unit tests for <see cref="HierarchicalChunkMapper"/> — the pure adapter from
/// <see cref="HierarchicalChunk"/> (chunking pipeline output) to <see cref="DocumentChunk"/>
/// (vector search / persistence model).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class HierarchicalChunkMapperTests
{
    private static readonly Guid DocumentId = Guid.NewGuid();

    private static ChunkMetadata Metadata(
        int page = 1,
        string? heading = "Setup",
        string elementType = "text",
        int charStart = 0,
        int charEnd = 10) => new()
        {
            Page = page,
            Heading = heading,
            ElementType = elementType,
            DocumentId = DocumentId,
            CharStart = charStart,
            CharEnd = charEnd
        };

    [Fact]
    public void ToDocumentChunks_ParentChunk_MapsParentChunkIdToNullAndLevelToZero()
    {
        // Arrange
        var parent = HierarchicalChunk.CreateParent("Section content", Metadata());

        // Act
        var result = HierarchicalChunkMapper.ToDocumentChunks(new List<HierarchicalChunk> { parent });

        // Assert
        result.Should().HaveCount(1);
        var mapped = result[0];
        mapped.ParentChunkId.Should().BeNull();
        mapped.Level.Should().Be((short)0);
    }

    [Fact]
    public void ToDocumentChunks_ChildChunk_MapsParentChunkIdAndLevelAndInheritsHeading()
    {
        // Arrange
        var parent = HierarchicalChunk.CreateParent("Section content", Metadata(heading: "Rules"));
        var child = HierarchicalChunk.CreateChild(
            "Child sentence",
            level: 2,
            Metadata(heading: "Rules"),
            parent.Id);

        // Act
        var result = HierarchicalChunkMapper.ToDocumentChunks(new List<HierarchicalChunk> { parent, child });

        // Assert
        var mappedParent = result.Single(c => c.Id.ToString("N") == parent.Id);
        var mappedChild = result.Single(c => c.Id.ToString("N") == child.Id);

        mappedChild.ParentChunkId.Should().Be(mappedParent.Id);
        mappedChild.Level.Should().Be((short)2);
        mappedChild.Heading.Should().Be("Rules");
    }

    [Fact]
    public void ToDocumentChunks_EmptyElementType_DefaultsToNarrativeText()
    {
        // Arrange
        var parent = HierarchicalChunk.CreateParent("Section content", Metadata(elementType: string.Empty));

        // Act
        var result = HierarchicalChunkMapper.ToDocumentChunks(new List<HierarchicalChunk> { parent });

        // Assert
        result[0].ElementType.Should().Be("NarrativeText");
    }

    [Fact]
    public void ToDocumentChunks_NonEmptyElementType_IsPreserved()
    {
        // Arrange
        var parent = HierarchicalChunk.CreateParent("Section content", Metadata(elementType: "table"));

        // Act
        var result = HierarchicalChunkMapper.ToDocumentChunks(new List<HierarchicalChunk> { parent });

        // Assert
        result[0].ElementType.Should().Be("table");
    }

    [Fact]
    public void ToDocumentChunks_GuidParseExactN_RoundTripsAdvancedChunkingServiceIdFormat()
    {
        // Arrange — HierarchicalChunk.Create assigns Id = Guid.NewGuid().ToString("N"),
        // exactly what AdvancedChunkingService emits for every chunk.
        var expectedGuid = Guid.NewGuid();
        var parent = HierarchicalChunk.CreateParent("Section content", Metadata());

        // Sanity: the factory-produced Id is itself a valid "N"-format Guid string.
        Guid.TryParseExact(parent.Id, "N", out var parsedFromFactory).Should().BeTrue();
        parsedFromFactory.ToString("N").Should().Be(parent.Id);

        // Act
        var result = HierarchicalChunkMapper.ToDocumentChunks(new List<HierarchicalChunk> { parent });

        // Assert
        result[0].Id.Should().Be(Guid.ParseExact(parent.Id, "N"));
        result[0].Id.Should().NotBe(expectedGuid); // sanity: not a coincidental match
    }

    [Fact]
    public void ToDocumentChunks_ChildParentChunkId_EqualsParentIdWithinSameList()
    {
        // Arrange
        var parent = HierarchicalChunk.CreateParent("Section content", Metadata());
        var child1 = HierarchicalChunk.CreateChild("Child 1", level: 2, Metadata(), parent.Id);
        var child2 = HierarchicalChunk.CreateChild("Child 2", level: 2, Metadata(), parent.Id);
        var input = new List<HierarchicalChunk> { parent, child1, child2 };

        // Act
        var result = HierarchicalChunkMapper.ToDocumentChunks(input);

        // Assert
        var mappedParent = result.Single(c => c.Text == "Section content");
        result.Where(c => c.Text.StartsWith("Child", StringComparison.Ordinal))
            .Should().AllSatisfy(c => c.ParentChunkId.Should().Be(mappedParent.Id));
    }

    [Fact]
    public void ToDocumentChunks_MapsAllRemainingFields()
    {
        // Arrange
        var metadata = Metadata(page: 3, heading: "Scoring", elementType: "text", charStart: 42, charEnd: 99);
        var parent = HierarchicalChunk.CreateParent("Scoring rules content", metadata);

        // Act
        var result = HierarchicalChunkMapper.ToDocumentChunks(new List<HierarchicalChunk> { parent });

        // Assert
        var mapped = result[0];
        mapped.Text.Should().Be("Scoring rules content");
        mapped.Page.Should().Be(3);
        mapped.CharStart.Should().Be(42);
        mapped.CharEnd.Should().Be(99);
        mapped.Heading.Should().Be("Scoring");
        mapped.Embedding.Should().BeEmpty();
    }

    [Fact]
    public void ToDocumentChunks_EmptyList_ReturnsEmptyList()
    {
        // Act
        var result = HierarchicalChunkMapper.ToDocumentChunks(new List<HierarchicalChunk>());

        // Assert
        result.Should().BeEmpty();
    }
}
