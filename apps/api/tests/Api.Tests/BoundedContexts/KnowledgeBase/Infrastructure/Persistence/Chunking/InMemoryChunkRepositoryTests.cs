using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Chunking;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Chunking;

/// <summary>
/// Unit tests for InMemoryChunkRepository.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class InMemoryChunkRepositoryTests
{
    private readonly InMemoryChunkRepository _repository;

    public InMemoryChunkRepositoryTests()
    {
        var loggerMock = new Mock<ILogger<InMemoryChunkRepository>>();
        _repository = new InMemoryChunkRepository(loggerMock.Object);
    }

    [Fact]
    public async Task SaveAsync_AddsChunkToRepository()
    {
        // Arrange
        var chunk = CreateTestChunk();

        // Act
        await _repository.SaveAsync(chunk);

        // Assert
        var retrieved = await _repository.GetByIdAsync(chunk.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(chunk.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existent-id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdsAsync_ReturnsMatchingChunks()
    {
        // Arrange
        var chunk1 = CreateTestChunk();
        var chunk2 = CreateTestChunk();
        var chunk3 = CreateTestChunk();

        await _repository.SaveBatchAsync(new List<HierarchicalChunk> { chunk1, chunk2, chunk3 });

        // Act
        var results = await _repository.GetByIdsAsync(new[] { chunk1.Id, chunk3.Id });

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(c => c.Id == chunk1.Id);
        results.Should().Contain(c => c.Id == chunk3.Id);
    }

    [Fact]
    public async Task SaveBatchAsync_AddsMultipleChunks()
    {
        // Arrange
        var chunks = new List<HierarchicalChunk>
        {
            CreateTestChunk(),
            CreateTestChunk(),
            CreateTestChunk()
        };

        // Act
        await _repository.SaveBatchAsync(chunks);

        // Assert
        _repository.Count.Should().Be(3);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsChildChunks()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var parentMetadata = new ChunkMetadata { DocumentId = documentId };
        var parent = HierarchicalChunk.CreateParent("Parent content", parentMetadata);

        var child1 = HierarchicalChunk.CreateChild("Child 1", 2, parentMetadata, parent.Id);
        var child2 = HierarchicalChunk.CreateChild("Child 2", 2, parentMetadata, parent.Id);

        parent.AddChild(child1.Id);
        parent.AddChild(child2.Id);

        await _repository.SaveBatchAsync(new List<HierarchicalChunk> { parent, child1, child2 });

        // Act
        var children = await _repository.GetChildrenAsync(parent.Id);

        // Assert
        children.Should().HaveCount(2);
        children.Should().Contain(c => c.Id == child1.Id);
        children.Should().Contain(c => c.Id == child2.Id);
    }

    [Fact]
    public async Task GetParentAsync_ReturnsParentChunk()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var parentMetadata = new ChunkMetadata { DocumentId = documentId };
        var parent = HierarchicalChunk.CreateParent("Parent content", parentMetadata);

        var child = HierarchicalChunk.CreateChild("Child content", 2, parentMetadata, parent.Id);
        parent.AddChild(child.Id);

        await _repository.SaveBatchAsync(new List<HierarchicalChunk> { parent, child });

        // Act
        var retrievedParent = await _repository.GetParentAsync(child.Id);

        // Assert
        retrievedParent.Should().NotBeNull();
        retrievedParent!.Id.Should().Be(parent.Id);
    }

    [Fact]
    public async Task GetParentAsync_WithRootChunk_ReturnsNull()
    {
        // Arrange
        var parent = CreateTestChunk();
        await _repository.SaveAsync(parent);

        // Act
        var result = await _repository.GetParentAsync(parent.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_RemovesAllDocumentChunks()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var metadata = new ChunkMetadata { DocumentId = documentId };

        var chunk1 = HierarchicalChunk.CreateParent("Content 1", metadata);
        var chunk2 = HierarchicalChunk.CreateParent("Content 2", metadata);

        var otherDocId = Guid.NewGuid();
        var otherMetadata = new ChunkMetadata { DocumentId = otherDocId };
        var chunk3 = HierarchicalChunk.CreateParent("Content 3", otherMetadata);

        await _repository.SaveBatchAsync(new List<HierarchicalChunk> { chunk1, chunk2, chunk3 });

        // Act
        await _repository.DeleteByDocumentIdAsync(documentId);

        // Assert
        _repository.Count.Should().Be(1);
        var remaining = await _repository.GetByIdAsync(chunk3.Id);
        remaining.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByDocumentIdAsync_ReturnsAllDocumentChunks()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var metadata = new ChunkMetadata { DocumentId = documentId };

        var chunk1 = HierarchicalChunk.CreateParent("Content 1", metadata);
        var chunk2 = HierarchicalChunk.CreateParent("Content 2", metadata);

        var otherDocId = Guid.NewGuid();
        var otherMetadata = new ChunkMetadata { DocumentId = otherDocId };
        var chunk3 = HierarchicalChunk.CreateParent("Content 3", otherMetadata);

        await _repository.SaveBatchAsync(new List<HierarchicalChunk> { chunk1, chunk2, chunk3 });

        // Act
        var results = await _repository.GetByDocumentIdAsync(documentId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(c => c.Metadata.DocumentId == documentId);
    }

    [Fact]
    public void Clear_RemovesAllChunks()
    {
        // Arrange
        var chunk = CreateTestChunk();
        _repository.SaveAsync(chunk).Wait();

        // Act
        _repository.Clear();

        // Assert
        _repository.Count.Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var chunk = CreateTestChunk();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _repository.SaveAsync(chunk, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static HierarchicalChunk CreateTestChunk()
    {
        var metadata = new ChunkMetadata
        {
            DocumentId = Guid.NewGuid(),
            Page = 1,
            ElementType = "text"
        };

        return HierarchicalChunk.CreateParent("Test content", metadata);
    }
}
