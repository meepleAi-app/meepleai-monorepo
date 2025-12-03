using System.Collections.Concurrent;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Chunking;

/// <summary>
/// ADR-016 Phase 1: In-memory implementation of IChunkRepository.
/// Used for development and testing. Will be replaced with EF Core implementation in later phases.
/// </summary>
public sealed class InMemoryChunkRepository : IChunkRepository
{
    private readonly ConcurrentDictionary<string, HierarchicalChunk> _chunks = new();
    private readonly ILogger<InMemoryChunkRepository> _logger;

    public InMemoryChunkRepository(ILogger<InMemoryChunkRepository> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<HierarchicalChunk?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _chunks.TryGetValue(id, out var chunk);
        return Task.FromResult(chunk);
    }

    /// <inheritdoc />
    public Task<List<HierarchicalChunk>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Thread-safe: Use TryGetValue to avoid race condition between ContainsKey and indexer
        var result = new List<HierarchicalChunk>();
        foreach (var id in ids)
        {
            if (_chunks.TryGetValue(id, out var chunk))
            {
                result.Add(chunk);
            }
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<List<HierarchicalChunk>> GetChildrenAsync(string parentId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var children = _chunks.Values
            .Where(c => c.ParentId == parentId)
            .ToList();

        return Task.FromResult(children);
    }

    /// <inheritdoc />
    public Task<HierarchicalChunk?> GetParentAsync(string childId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_chunks.TryGetValue(childId, out var childChunk))
            return Task.FromResult<HierarchicalChunk?>(null);

        if (childChunk.ParentId == null)
            return Task.FromResult<HierarchicalChunk?>(null);

        _chunks.TryGetValue(childChunk.ParentId, out var parentChunk);
        return Task.FromResult(parentChunk);
    }

    /// <inheritdoc />
    public Task SaveAsync(HierarchicalChunk chunk, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _chunks[chunk.Id] = chunk;
        _logger.LogDebug("Saved chunk {ChunkId}", chunk.Id);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveBatchAsync(List<HierarchicalChunk> chunks, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var chunk in chunks)
        {
            _chunks[chunk.Id] = chunk;
        }

        _logger.LogDebug("Saved {Count} chunks in batch", chunks.Count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var chunksToRemove = _chunks.Values
            .Where(c => c.Metadata.DocumentId == documentId)
            .Select(c => c.Id)
            .ToList();

        foreach (var id in chunksToRemove)
        {
            _chunks.TryRemove(id, out _);
        }

        _logger.LogDebug("Deleted {Count} chunks for document {DocumentId}", chunksToRemove.Count, documentId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<HierarchicalChunk>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var chunks = _chunks.Values
            .Where(c => c.Metadata.DocumentId == documentId)
            .ToList();

        return Task.FromResult(chunks);
    }

    /// <summary>
    /// Gets the total count of stored chunks (for testing).
    /// </summary>
    public int Count => _chunks.Count;

    /// <summary>
    /// Clears all stored chunks (for testing).
    /// </summary>
    public void Clear() => _chunks.Clear();
}
