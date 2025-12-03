using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;

/// <summary>
/// Unit tests for ParentChunkResolver.
/// ADR-016 Phase 4: Parent chunk resolution for hierarchical retrieval.
/// </summary>
public class ParentChunkResolverTests
{
    private readonly Mock<IChunkRepository> _chunkRepositoryMock;
    private readonly Mock<ILogger<ParentChunkResolver>> _loggerMock;
    private readonly ParentChunkResolver _resolver;

    public ParentChunkResolverTests()
    {
        _chunkRepositoryMock = new Mock<IChunkRepository>();
        _loggerMock = new Mock<ILogger<ParentChunkResolver>>();

        _resolver = new ParentChunkResolver(
            _chunkRepositoryMock.Object,
            _loggerMock.Object);
    }

    private static ChunkMetadata CreateMetadata(Guid documentId, int page = 1)
    {
        return new ChunkMetadata
        {
            DocumentId = documentId,
            Page = page,
            ElementType = "text"
        };
    }

    [Fact]
    public async Task ResolveParentsAsync_WithEmptyChildIds_ReturnsEmptyList()
    {
        // Act
        var result = await _resolver.ResolveParentsAsync(new List<string>());

        // Assert
        result.Should().BeEmpty();
        _chunkRepositoryMock.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveParentsAsync_WithNullChildIds_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _resolver.ResolveParentsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ResolveParentsAsync_WithChildHavingParent_ResolvesParentContent()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var parentChunk = HierarchicalChunk.CreateParent(
            "Parent content with more context",
            CreateMetadata(documentId));

        var childChunk = HierarchicalChunk.CreateChild(
            "Child content",
            level: 2,
            CreateMetadata(documentId),
            parentChunk.Id);

        _chunkRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
            {
                var idList = ids.ToList();
                var results = new List<HierarchicalChunk>();
                if (idList.Contains(childChunk.Id)) results.Add(childChunk);
                if (idList.Contains(parentChunk.Id)) results.Add(parentChunk);
                return results;
            });

        // Act
        var result = await _resolver.ResolveParentsAsync(new List<string> { childChunk.Id });

        // Assert
        result.Should().HaveCount(1);
        result[0].ChildId.Should().Be(childChunk.Id);
        result[0].ParentId.Should().Be(parentChunk.Id);
        result[0].ParentContent.Should().Be("Parent content with more context");
    }

    [Fact]
    public async Task ResolveParentsAsync_WithRootChunk_ReturnsEmptyList()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var rootChunk = HierarchicalChunk.CreateParent(
            "Root chunk content",
            CreateMetadata(documentId));

        _chunkRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { rootChunk });

        // Act
        var result = await _resolver.ResolveParentsAsync(new List<string> { rootChunk.Id });

        // Assert - root chunks have no parent, so no resolved parents
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveParentsAsync_WithMissingChild_ReturnsEmptyList()
    {
        // Arrange
        var missingChildId = "missing-child";

        _chunkRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk>());

        // Act
        var result = await _resolver.ResolveParentsAsync(new List<string> { missingChildId });

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveParentsAsync_WithMissingParent_SkipsOrphanedChild()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var missingParentId = "missing-parent-" + Guid.NewGuid().ToString("N");

        // Create a child that references a missing parent
        var childChunk = HierarchicalChunk.CreateChild(
            "Orphan child content",
            level: 2,
            CreateMetadata(documentId),
            missingParentId);

        _chunkRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
            {
                var idList = ids.ToList();
                if (idList.Contains(childChunk.Id))
                    return new List<HierarchicalChunk> { childChunk };
                return new List<HierarchicalChunk>();
            });

        // Act
        var result = await _resolver.ResolveParentsAsync(new List<string> { childChunk.Id });

        // Assert - parent not found, so child is skipped
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveParentsAsync_WithMultipleChildren_ResolvesAll()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var parent = HierarchicalChunk.CreateParent(
            "Shared parent content",
            CreateMetadata(documentId));

        var child1 = HierarchicalChunk.CreateChild(
            "Child 1 content",
            level: 2,
            CreateMetadata(documentId),
            parent.Id);

        var child2 = HierarchicalChunk.CreateChild(
            "Child 2 content",
            level: 2,
            CreateMetadata(documentId),
            parent.Id);

        _chunkRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
            {
                var idList = ids.ToList();
                var results = new List<HierarchicalChunk>();
                if (idList.Contains(child1.Id)) results.Add(child1);
                if (idList.Contains(child2.Id)) results.Add(child2);
                if (idList.Contains(parent.Id)) results.Add(parent);
                return results;
            });

        // Act
        var result = await _resolver.ResolveParentsAsync(new List<string> { child1.Id, child2.Id });

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.ChildId == child1.Id && r.ParentContent == "Shared parent content");
        result.Should().Contain(r => r.ChildId == child2.Id && r.ParentContent == "Shared parent content");
    }

    [Fact]
    public async Task ResolveParentAsync_WithValidChild_ReturnsParent()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var parent = HierarchicalChunk.CreateParent(
            "Parent content",
            CreateMetadata(documentId));

        var child = HierarchicalChunk.CreateChild(
            "Child content",
            level: 2,
            CreateMetadata(documentId),
            parent.Id);

        _chunkRepositoryMock
            .Setup(r => r.GetByIdAsync(child.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(child);

        _chunkRepositoryMock
            .Setup(r => r.GetParentAsync(child.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

        // Act
        var result = await _resolver.ResolveParentAsync(child.Id);

        // Assert
        result.Should().NotBeNull();
        result!.ChildId.Should().Be(child.Id);
        result.ParentId.Should().Be(parent.Id);
        result.ParentContent.Should().Be("Parent content");
    }

    [Fact]
    public async Task ResolveParentAsync_WithRootChunk_ReturnsNull()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var root = HierarchicalChunk.CreateParent(
            "Root content",
            CreateMetadata(documentId));

        _chunkRepositoryMock
            .Setup(r => r.GetByIdAsync(root.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(root);

        // Act
        var result = await _resolver.ResolveParentAsync(root.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveParentAsync_WithEmptyChunkId_ThrowsArgumentException()
    {
        // Act
        var act = () => _resolver.ResolveParentAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveParentAsync_WithMissingChunk_ReturnsNull()
    {
        // Arrange
        _chunkRepositoryMock
            .Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((HierarchicalChunk?)null);

        // Act
        var result = await _resolver.ResolveParentAsync("missing");

        // Assert
        result.Should().BeNull();
    }
}
